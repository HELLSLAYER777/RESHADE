// ReShade Bridge - a mod for MFG SUPER MOTION (ModKit\README.md).
// Runs ReShade inside the frame-generation engine, so its effects and its overlay apply to the generated frames, and
// makes the overlay usable: the overlay key (Home) pressed over the game opens the ReShade overlay on the engine's
// output window, and pressed again hands the keyboard and mouse back to the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MfgSuperMotion.Mods;

namespace ReShadeBridge
{
    public sealed class ReShadeBridgeMod : IMod
    {
        private const string PageId = "reshade";
        private static readonly string[] ProxyNames = { "dxgi", "d3d11", "d3d12" };

        private IModHost host = null!;
        private BridgeSettings cfg = new();
        private OverlayBridge bridge = null!;
        private string lang = "en";

        private readonly List<Action> refreshers = new();
        private TextBox? dllBox, folderBox, engineBox;
        private ComboBox? keyBox, proxyBox;
        private CheckBox? enabledBox, frontBox;

        private string? noticeKey;
        private object[] noticeArgs = Array.Empty<object>();
        private string logTail = "";
        private string lastTickError = "";

        public void Load(IModHost host)
        {
            this.host = host;
            lang = host.Language;
            cfg = BridgeSettings.Load(host.DataFolder);
            if (Array.IndexOf(ProxyNames, cfg.Proxy) < 0) cfg.Proxy = "dxgi";
            cfg.OverlayKey = OverlayKeys.All[OverlayKeys.IndexOf(cfg.OverlayKey)].Vk;

            bridge = new OverlayBridge(() => host.IsMotionRunning, Notice, host.Log) { Settings = cfg };
            bridge.EngineFolderSeen += folder =>
            {
                // The first time the engine runs, remember where it lives so Install knows.
                if (!string.IsNullOrWhiteSpace(cfg.EngineFolder)) return;
                cfg.EngineFolder = folder;
                cfg.Save(host.DataFolder);
                if (engineBox != null && string.IsNullOrWhiteSpace(engineBox.Text)) engineBox.Text = folder;
            };

            host.LanguageChanged += (_, code) => { lang = code; Refresh(); };

            host.AddPage(new ModPage
            {
                Id = PageId,
                Title = "ReShade",
                // Three stacked layers.
                Icon = "M 12,2 L 2,7 L 12,12 L 22,7 Z M 2,12 L 12,17 L 22,12 L 20.5,11.2 L 12,15.4 L 3.5,11.2 Z M 2,17 L 12,22 L 22,17 L 20.5,16.2 L 12,20.4 L 3.5,16.2 Z",
                Content = MakePage
            });
            host.AddToPage(Pages.Display, MakeCard(), 600);

            // The key is watched every 30 ms; what is shown on the page is brought up to date once a second.
            var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            tick.Tick += (_, _) =>
            {
                try { bridge.Tick(); }
                catch (Exception ex)
                {
                    if (ex.Message != lastTickError) { lastTickError = ex.Message; host.Log("bridge tick: " + ex); }
                }
            };
            tick.Start();

            var ui = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ui.Tick += (_, _) => Refresh();
            ui.Start();

            // If the app closes while the overlay is open, give the engine window back its click-through style.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { bridge.Shutdown(); } catch { } };

            host.Log("ReShade Bridge loaded (overlay key: " + OverlayKeys.NameOf(cfg.OverlayKey) + ")");
        }

        // ------------------------------------------------------------------ small helpers

        private string S(string key, params object[] args) => Strings.Get(lang, key, args);

        private void Refresh()
        {
            foreach (var r in refreshers) r();
        }

        private void Notice(string key, object[] args)
        {
            noticeKey = key;
            noticeArgs = args;
            Refresh();
        }

        private void Show(Outcome o) => Notice(o.Key, o.Args);

        private static string Clean(string? text) => (text ?? "").Trim().Trim('"');

        private TextBlock Text(Func<string> text, double size, FontWeight weight, bool muted)
        {
            var tb = new TextBlock { FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
            if (muted) tb.Classes.Add("muted");
            refreshers.Add(() => tb.Text = text());
            tb.Text = text();
            return tb;
        }

        private Button Btn(string key, Action onClick)
        {
            var b = new Button { Margin = new Thickness(0, 0, 8, 8) };
            b.Click += (_, _) =>
            {
                try { onClick(); }
                catch (Exception ex) { host.Log(key + ": " + ex); Notice("err_io", new object[] { ex.Message }); }
            };
            refreshers.Add(() => b.Content = S(key));
            b.Content = S(key);
            return b;
        }

        private Button BtnAsync(string key, Func<Task> onClick)
        {
            var b = new Button { Margin = new Thickness(0, 0, 8, 8) };
            b.Click += async (_, _) =>
            {
                try { await onClick(); }
                catch (Exception ex) { host.Log(key + ": " + ex); Notice("err_io", new object[] { ex.Message }); }
            };
            refreshers.Add(() => b.Content = S(key));
            b.Content = S(key);
            return b;
        }

        private CheckBox Check(string key, bool value, Action onClick)
        {
            var c = new CheckBox { IsChecked = value };
            c.Click += (_, _) => onClick();
            refreshers.Add(() => c.Content = S(key));
            c.Content = S(key);
            return c;
        }

        private static Control Row(TextBox box, params Button[] buttons)
        {
            var columns = "*" + string.Concat(buttons.Select(_ => ",Auto"));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
            box.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(box);
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].Margin = new Thickness(8, 0, 0, 0);
                buttons[i].VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(buttons[i], i + 1);
                grid.Children.Add(buttons[i]);
            }
            return grid;
        }

        // ------------------------------------------------------------------ status lines

        private string EngineLine() => bridge.Engine is { } e
            ? S("stEngineRun", e.Pid, Path.GetFileName(e.ExePath))
            : S("stEngineOff");

        private string WindowLine() => bridge.Engine == null ? "" : S(bridge.HasWindow ? "stWindow" : "stNoWindow");

        private string FileLine() => ReShadeInstaller.InstalledVersion(cfg) is { } v
            ? S("stFile", v, ReShadeInstaller.ProxyFile(cfg))
            : S("stNoFile");

        private string LoadedLine() => bridge.Engine == null
            ? ""
            : bridge.ReShadeLoaded switch { true => S("stLoaded"), false => S("stNotLoaded"), null => S("stUnknown") };

        private string OverlayLine() => S(bridge.Interactive ? "stOverlayOpen" : "stOverlayClosed");

        // ------------------------------------------------------------------ the page

        private Control MakePage()
        {
            var panel = new StackPanel { Spacing = 12 };

            panel.Children.Add(Text(() => S("title"), 22, FontWeight.SemiBold, false));
            panel.Children.Add(Text(() => S("intro", OverlayKeys.NameOf(cfg.OverlayKey)), 13, FontWeight.Normal, true));

            var statusCard = new Border { Classes = { "card" } };
            var status = new StackPanel { Spacing = 4 };
            foreach (var line in new Func<string>[] { EngineLine, WindowLine, FileLine, LoadedLine, OverlayLine })
            {
                var tb = Text(line, 13, FontWeight.Normal, false);
                var read = line;
                refreshers.Add(() => tb.IsVisible = read().Length > 0);
                status.Children.Add(tb);
            }
            statusCard.Child = status;
            panel.Children.Add(statusCard);

            dllBox = new TextBox { Text = cfg.ReShadeDll };
            folderBox = new TextBox { Text = cfg.ReShadeFolder };
            engineBox = new TextBox { Text = cfg.EngineFolder };

            panel.Children.Add(Text(() => S("lblDll"), 12, FontWeight.Normal, true));
            panel.Children.Add(Row(dllBox,
                BtnAsync("btnBrowse", () => PickFile(dllBox)),
                Btn("btnGet", () => host.OpenUrl("https://reshade.me"))));

            panel.Children.Add(Text(() => S("lblFolder"), 12, FontWeight.Normal, true));
            panel.Children.Add(Row(folderBox, BtnAsync("btnBrowse", () => PickFolder(folderBox))));

            panel.Children.Add(Text(() => S("lblEngine"), 12, FontWeight.Normal, true));
            panel.Children.Add(Row(engineBox,
                BtnAsync("btnBrowse", () => PickFolder(engineBox)),
                Btn("btnDetect", DoDetect)));

            keyBox = new ComboBox
            {
                ItemsSource = OverlayKeys.All.Select(k => k.Name).ToList(),
                SelectedIndex = OverlayKeys.IndexOf(cfg.OverlayKey),
                MinWidth = 150
            };
            proxyBox = new ComboBox
            {
                ItemsSource = ProxyNames.Select(n => n + ".dll").ToList(),
                SelectedIndex = Math.Max(0, Array.IndexOf(ProxyNames, cfg.Proxy)),
                MinWidth = 150
            };
            var combos = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            combos.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children = { Text(() => S("lblKey"), 12, FontWeight.Normal, true), keyBox }
            });
            combos.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children = { Text(() => S("lblLoad"), 12, FontWeight.Normal, true), proxyBox }
            });
            panel.Children.Add(combos);
            panel.Children.Add(Text(() => S("hintLoad"), 12, FontWeight.Normal, true));

            enabledBox = Check("chkEnabled", cfg.Enabled, DoToggles);
            frontBox = Check("chkFront", cfg.OnlyWhenGameInFront, DoToggles);
            panel.Children.Add(enabledBox);
            panel.Children.Add(frontBox);

            var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(Btn("btnInstall", DoInstall));
            buttons.Children.Add(Btn("btnRemove", DoRemove));
            buttons.Children.Add(Btn("btnOpen", () => bridge.RequestToggle()));
            buttons.Children.Add(Btn("btnLog", DoShowLog));
            panel.Children.Add(buttons);

            panel.Children.Add(Text(() => noticeKey == null ? "" : S(noticeKey, noticeArgs), 13, FontWeight.Normal, false));

            var logCard = new Border { Classes = { "card" } };
            var log = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace") };
            logCard.Child = log;
            refreshers.Add(() => { log.Text = logTail; logCard.IsVisible = logTail.Length > 0; });
            logCard.IsVisible = false;
            panel.Children.Add(logCard);

            Refresh();
            return panel;
        }

        // ------------------------------------------------------------------ the small card on Super Motion

        private Control MakeCard()
        {
            var card = new Border { Classes = { "card" } };
            var panel = new StackPanel { Spacing = 6 };

            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var title = Text(() => S("cardTitle"), 16, FontWeight.SemiBold, false);
            title.VerticalAlignment = VerticalAlignment.Center;
            var settings = Btn("btnSettings", () => host.ShowPage(host.ModId + "/" + PageId));
            settings.Margin = new Thickness(0);
            Grid.SetColumn(settings, 1);
            head.Children.Add(title);
            head.Children.Add(settings);
            panel.Children.Add(head);

            panel.Children.Add(Text(() => S("cardLine", OverlayKeys.NameOf(cfg.OverlayKey)) + " · " + CardState(), 13, FontWeight.Normal, true));

            card.Child = panel;
            return card;
        }

        private string CardState()
        {
            if (bridge.Engine != null)
                return bridge.ReShadeLoaded switch { true => S("cardLoaded"), false => S("cardNotLoaded"), null => "ReShade ?" };
            return ReShadeInstaller.InstalledVersion(cfg) is { } v ? "ReShade " + v : "ReShade -";
        }

        // ------------------------------------------------------------------ what the buttons do

        private void ReadFields()
        {
            cfg.ReShadeDll = Clean(dllBox?.Text);
            cfg.ReShadeFolder = Clean(folderBox?.Text);
            cfg.EngineFolder = Clean(engineBox?.Text);
            if (proxyBox != null) cfg.Proxy = ProxyNames[Math.Clamp(proxyBox.SelectedIndex, 0, ProxyNames.Length - 1)];
            if (keyBox != null) cfg.OverlayKey = OverlayKeys.All[Math.Clamp(keyBox.SelectedIndex, 0, OverlayKeys.All.Length - 1)].Vk;
            cfg.Enabled = enabledBox?.IsChecked == true;
            cfg.OnlyWhenGameInFront = frontBox?.IsChecked == true;
        }

        private void DoToggles()
        {
            ReadFields();
            cfg.Save(host.DataFolder);
            Notice("info_saved", Array.Empty<object>());
        }

        private void DoInstall()
        {
            ReadFields();
            var outcome = ReShadeInstaller.Install(cfg, bridge.Engine?.ExePath);
            cfg.Save(host.DataFolder);
            host.Log("install: " + outcome.Key);
            Show(outcome);
        }

        private void DoRemove()
        {
            ReadFields();
            var outcome = ReShadeInstaller.Remove(cfg);
            cfg.Save(host.DataFolder);
            host.Log("remove: " + outcome.Key);
            Show(outcome);
        }

        private void DoDetect()
        {
            var first = EngineLocator.FindRunning(out _).FirstOrDefault();
            if (first != null && !string.IsNullOrEmpty(first.Folder))
            {
                if (engineBox != null) engineBox.Text = first.Folder;
                Notice("info_detected", new object[] { first.Folder });
                return;
            }
            var guess = EngineLocator.GuessFolderOnDisk();
            if (guess != null)
            {
                if (engineBox != null) engineBox.Text = guess;
                Notice("info_guessed", new object[] { guess });
            }
            else Notice("info_nodetect", Array.Empty<object>());
        }

        private void DoShowLog()
        {
            var folder = Clean(engineBox?.Text);
            var path = folder.Length == 0 ? "" : Path.Combine(folder, "ReShade.log");
            if (path.Length == 0 || !File.Exists(path))
            {
                logTail = "";
                Notice("info_nolog", Array.Empty<object>());
                return;
            }
            try
            {
                // ReShade keeps the log open while the engine runs, so open it in a way that allows that.
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
                logTail = string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 30)));
            }
            catch (Exception ex) { logTail = ex.Message; }
            Refresh();
        }

        private async Task PickFile(TextBox box)
        {
            var files = await host.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("DLL") { Patterns = new[] { "*.dll" } } }
            });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) box.Text = path;
        }

        private async Task PickFolder(TextBox box)
        {
            var folders = await host.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
            if (folders.Count == 0) return;
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) box.Text = path;
        }
    }
}
