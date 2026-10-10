using Simitone.Windows.GameLocator;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Simitone.Launcher
{
    /// <summary>
    /// Picks the game folder, edits the start-up settings (the same names and descriptions as Options > Settings in game) and starts Simitone.
    /// The settings are stored in the game's own simitone.ini and config.ini, so the game and the launcher always agree.
    /// </summary>
    public class LauncherForm : Form
    {
        private readonly IniFile SimitoneIni;
        private readonly IniFile ConfigIni;
        private readonly IniFile LauncherIni;
        private readonly List<SettingDef> Defs = LauncherSettings.All();
        private readonly Dictionary<SettingDef, Control> Editors = new Dictionary<SettingDef, Control>();

        private TextBox GameFolder;
        private Label GameFolderState;
        private TextBox Extra;
        private ComboBox SmokeMinutes;
        private Label Status;
        private readonly string LauncherDir = AppDomain.CurrentDomain.BaseDirectory;

        public LauncherForm()
        {
            Directory.CreateDirectory(LauncherPaths.UserDir);
            SimitoneIni = new IniFile(Path.Combine(LauncherPaths.UserDir, "simitone.ini"));
            ConfigIni = new IniFile(Path.Combine(LauncherPaths.UserDir, "config.ini"));
            LauncherIni = new IniFile(Path.Combine(LauncherPaths.UserDir, "launcher.ini"));

            Text = "Simitone Launcher";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 640);
            MinimumSize = new Size(560, 480);
            Font = new Font("Segoe UI", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildGameFolderRow(), 0, 0);
            root.Controls.Add(BuildTabs(), 0, 1);
            root.Controls.Add(BuildButtons(), 0, 2);
            Status = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            root.Controls.Add(Status, 0, 3);

            GameFolder.Text = InitialGameFolder();
            RefreshGameFolderState();
            Extra.Text = LauncherIni.Get("ExtraSwitches", "");
            FormClosing += (s, e) => SaveAll();
        }

        private string InitialGameFolder()
        {
            var saved = SimitoneIni.Get("GamePath", "");
            if (GameInstall.IsValid(saved)) return saved;
            try { return new WindowsLocator().FindTheSims1()?.Replace('/', '\\') ?? ""; } catch (Exception) { return ""; }
        }

        private Control BuildGameFolderRow()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = "The Sims 1 folder", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 0, 8, 0) }, 0, 0);
            GameFolder = new TextBox { Dock = DockStyle.Fill };
            GameFolder.TextChanged += (s, e) => RefreshGameFolderState();
            panel.Controls.Add(GameFolder, 1, 0);
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Choose the folder of The Sims 1 (it contains GameData\\Behavior.iff).";
                    dialog.ShowNewFolderButton = false;
                    if (dialog.ShowDialog(this) == DialogResult.OK) GameFolder.Text = dialog.SelectedPath;
                }
            };
            panel.Controls.Add(browse, 2, 0);
            GameFolderState = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };
            panel.Controls.Add(GameFolderState, 1, 1);
            return panel;
        }

        private void RefreshGameFolderState()
        {
            var text = GameFolder.Text.Trim();
            if (text.Length == 0) GameFolderState.Text = "Not found. Choose the folder that contains GameData\\Behavior.iff.";
            else if (!GameInstall.IsValid(text)) GameFolderState.Text = "This folder does not contain GameData\\Behavior.iff.";
            else GameFolderState.Text = GameInstall.IsLegacyCollection(text) ? "Steam Legacy Collection" : "Classic install";
        }

        private Control BuildTabs()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            foreach (var section in LauncherSettings.Sections)
            {
                var page = new TabPage(section) { UseVisualStyleBackColor = true };
                var flow = NewFlow();
                foreach (var def in Defs.Where(d => d.Section == section)) flow.Controls.Add(BuildEditor(def, flow));
                page.Controls.Add(flow);
                tabs.TabPages.Add(page);
            }
            tabs.TabPages.Add(BuildToolsPage());
            return tabs;
        }

        private FlowLayoutPanel NewFlow()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
        }

        private Control BuildEditor(SettingDef def, FlowLayoutPanel flow)
        {
            var ini = def.Target == IniTarget.Simitone ? SimitoneIni : ConfigIni;
            var selected = LauncherSettings.ChoiceFor(def, ini.Get(def.Key, null));
            var holder = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            Control editor;
            if (def.IsToggle)
            {
                editor = new CheckBox { Text = def.Label, AutoSize = true, Checked = selected == 1, Font = new Font(Font, FontStyle.Bold) };
            }
            else
            {
                holder.Controls.Add(new Label { Text = def.Label, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
                combo.Items.AddRange(def.Choices);
                combo.SelectedIndex = Math.Max(0, Math.Min(def.Choices.Length - 1, selected));
                editor = combo;
            }
            holder.Controls.Add(editor);
            if (!string.IsNullOrEmpty(def.Help))
            {
                var help = new Label { Text = def.Help, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(620, 0) };
                holder.Controls.Add(help);
                flow.SizeChanged += (s, e) => help.MaximumSize = new Size(Math.Max(200, flow.ClientSize.Width - 40), 0);
            }
            Editors[def] = editor;
            return holder;
        }

        private TabPage BuildToolsPage()
        {
            var page = new TabPage("Tools") { UseVisualStyleBackColor = true };
            var flow = NewFlow();

            flow.Controls.Add(new Label { Text = "Extra command line switches", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
            Extra = new TextBox { Width = 460 };
            flow.Controls.Add(Extra);
            flow.Controls.Add(Hint("Added to every start, e.g. -3d -aa -hz144 -path \"D:\\Games\\The Sims\". A switch wins over the setting above for that start."));

            flow.Controls.Add(ToolButton("Open the settings folder", () => Open(LauncherPaths.UserDir)));
            flow.Controls.Add(Hint("Documents\\Simitone: simitone.ini, config.ini, Simitone's copy of your neighbourhoods and the reports."));

            flow.Controls.Add(ToolButton("Import my saves again", ReimportSaves));
            flow.Controls.Add(Hint("Simitone works on a copy of the original game's saves. This sets its copy aside (never deletes it) and copies the original saves again on the next start."));

            flow.Controls.Add(new Label { Text = "Crash test (smoke test)", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 14, 3, 3) });
            SmokeMinutes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            SmokeMinutes.Items.AddRange(new object[] { "30 game minutes per lot", "60 game minutes per lot", "120 game minutes per lot", "240 game minutes per lot" });
            SmokeMinutes.SelectedIndex = 1;
            flow.Controls.Add(SmokeMinutes);
            flow.Controls.Add(ToolButton("Run the smoke test", () => Start(true)));
            flow.Controls.Add(Hint("Plays every lot at top speed with random Sim actions in its own folder (your neighbourhoods are not touched) and writes a report."));
            flow.Controls.Add(ToolButton("Open the smoke test reports", () => Open(Path.Combine(LauncherPaths.UserDir, "smoke", "reports"))));

            page.Controls.Add(flow);
            return page;
        }

        private Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(620, 0), Margin = new Padding(3, 2, 3, 10) };
        }

        private Button ToolButton(string text, Action click)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3, 8, 3, 0) };
            b.Click += (s, e) => click();
            return b;
        }

        private Control BuildButtons()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            var play = new Button { Text = "Play", Width = 140, Height = 40, Font = new Font(Font.FontFamily, 12f, FontStyle.Bold) };
            play.Click += (s, e) => Start(false);
            var save = new Button { Text = "Save", Width = 90, Height = 40 };
            save.Click += (s, e) => { SaveAll(); Status.Text = "Settings saved."; };
            panel.Controls.Add(play);
            panel.Controls.Add(save);
            AcceptButton = play;
            return panel;
        }

        private void SaveAll()
        {
            try
            {
                foreach (var pair in Editors)
                {
                    var def = pair.Key;
                    int index = pair.Value is CheckBox cb ? (cb.Checked ? 1 : 0) : ((ComboBox)pair.Value).SelectedIndex;
                    if (index < 0) continue;
                    (def.Target == IniTarget.Simitone ? SimitoneIni : ConfigIni).Set(def.Key, LauncherSettings.ValueFor(def, index));
                }
                var folder = GameFolder.Text.Trim();
                SimitoneIni.Set("GamePath", GameInstall.IsValid(folder) ? folder.Replace('\\', '/') : "");
                LauncherIni.Set("ExtraSwitches", Extra.Text.Trim());
                SimitoneIni.Save();
                ConfigIni.Save();
                LauncherIni.Save();
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "Could not save the settings: " + e.Message, "Simitone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Start(bool smoke)
        {
            var folder = GameFolder.Text.Trim();
            if (!GameInstall.IsValid(folder))
            {
                MessageBox.Show(this, "Choose the folder of The Sims 1 first (it contains GameData\\Behavior.iff).", "Simitone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var exe = LauncherPaths.FindGame(LauncherDir, null);
            if (exe == null)
            {
                MessageBox.Show(this, "Simitone.exe was not found next to the launcher (" + LauncherDir + ").", "Simitone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveAll();
            int minutes = 60;
            if (SmokeMinutes != null) minutes = new[] { 30, 60, 120, 240 }[Math.Max(0, SmokeMinutes.SelectedIndex)];
            var args = LaunchArguments.Build(Extra.Text, smoke, minutes);
            try
            {
                Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false });
                Status.Text = "Started " + Path.GetFileName(exe) + (args.Length > 0 ? " " + args : "");
                if (!smoke) Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "Could not start the game: " + e.Message, "Simitone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Open(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception e) { Status.Text = "Could not open " + folder + ": " + e.Message; }
        }

        private void ReimportSaves()
        {
            try
            {
                File.WriteAllText(Path.Combine(LauncherPaths.UserDir, "reimport-saves.flag"),
                    "Simitone will set aside its neighbourhood copies and import them again from the original game on the next start.");
                Status.Text = "Done: Simitone will import your saves again the next time it starts.";
            }
            catch (Exception e) { Status.Text = "Could not request the import: " + e.Message; }
        }
    }
}
