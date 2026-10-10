using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;
using Simitone.Windows.GameLocator;
using Simitone.Windows.Utils;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Simitone.Windows
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;

            ILocator gameLocator;
            bool linux = pid == PlatformID.MacOSX || pid == PlatformID.Unix;
            if (linux && Directory.Exists("/Users"))
                gameLocator = new MacOSLocator();
            else if (linux)
                gameLocator = new LinuxLocator();
            else
                gameLocator = new WindowsLocator();

            var path = gameLocator.FindTheSims1();
            bool pathGiven = false;

            //the user folder must be set before anything reads GlobalSettings: with -lang or -hz, config.ini used to be
            //created next to the exe instead of in Documents/Simitone.
            FSOEnvironment.UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone/").Replace('\\', '/');
            Directory.CreateDirectory(FSOEnvironment.UserDir);

            //start-up options come from simitone.ini (Options > Settings); command line switches override them for this run.
            var startup = Simitone.Client.Utils.SimitoneSettings.Default;
            var useDX = !linux && startup.UseDirectX;
            FSOEnvironment.Enable3D = startup.Enable3D;
            FSOEnvironment.SoftwareKeyboard = startup.TouchUI;
            FSOEnvironment.NoSound = startup.NoSound;
            bool ide = false;
            bool aa = startup.AntiAlias;
            bool jit = false;
            #region User resolution parmeters

            FSOEnvironment.Args = string.Join(" ", args);

            foreach (var arg in args)
            {
                if (arg[0] == '-')
                {
                    var cmd = arg.Substring(1);
                    if (cmd.StartsWith("lang"))
                    {
                        GlobalSettings.Default.LanguageCode = byte.Parse(cmd.Substring(4));
                    }
                    else if (cmd.StartsWith("hz")) GlobalSettings.Default.TargetRefreshRate = int.Parse(cmd.Substring(2));
                    else
                    {
                        //normal style param
                        switch (cmd)
                        {
                            case "ide":
                                ide = true;
                                break;
                            case "3d":
                                FSOEnvironment.Enable3D = true;
                                break;
                            case "aa":
                                aa = true;
                                break;
                            case "jit":
                                jit = true;
                                break;
                            case "dx":
                            case "dx11":
                                useDX = true;
                                break;
                            case "gl":
                            case "ogl":
                                useDX = false;
                                break;
                            case "touch":
                                FSOEnvironment.SoftwareKeyboard = true;
                                break;
                            case "nosound":
                                FSOEnvironment.NoSound = true;
                                break;
                        }
                    }
                }
            }
            #endregion

            //game folder: -path on the command line, else the one saved in Options > Settings > Debug, else auto-detected.
            Simitone.Client.Utils.SimitoneSettingsRegistry.BrowseGameFolder = () =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Choose the folder of The Sims 1 (it contains GameData\\Behavior.iff).";
                    dialog.ShowNewFolderButton = false;
                    return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
                }
            };
            Simitone.Client.Utils.SimitoneSettingsRegistry.IsValidGameFolder = GameInstall.IsValid;
            var origin = path == null ? "not found" : "auto-detected";
            if (!string.IsNullOrWhiteSpace(startup.GamePath) && GameInstall.IsValid(startup.GamePath.Trim().Replace('\\', '/')))
            {
                path = PathArgument.Normalise(startup.GamePath.Trim());
                pathGiven = true;
                origin = "saved in settings";
            }
            if (PathArgument.TryGet(args, out var argPath))
            {
                if (argPath != null)
                {
                    path = PathArgument.Normalise(argPath);
                    pathGiven = true;
                    origin = "command line (-path)";
                }
                else pathGiven = false; //-path with nothing after it: fall back to the settings/auto-detected folder
            }
            if (path != null) Simitone.Client.Utils.GameSourceInfo.Set(path, origin, GameInstall.IsLegacyCollection(path));

            if (path == null || (pathGiven && !GameInstall.IsValid(path)))
            {
                //nothing to start with: say why instead of closing silently.
                MessageBox.Show("Simitone could not find The Sims 1 (no GameData/Behavior.iff in " + (path ?? "any known install location") + ").\n\n" +
                    "Start Simitone with -path \"C:\\path\\to\\The Sims\" (or set the folder in Options > Settings > Debug) to point it at the game folder " +
                    "(for Steam: ...\\steamapps\\common\\The Sims Legacy Collection).", "Simitone", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //original-game saves: where the install keeps them (Steam's Legacy Collection keeps them outside the install),
            //and a requested re-import has to happen before any neighbourhood is loaded.
            Simitone.Client.Utils.SaveImport.ApplyPending();
            FSO.Content.TS1.TS1NeighborhoodProvider.SaveImportRoots = GameInstall.SaveRoots(path);

            useDX = MonogameLinker.Link(useDX);

            FSO.Files.ImageLoaderHelpers.BitmapFunction = BitmapReader;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            FSOEnvironment.SoftwareDepth = false;
            FSOEnvironment.UseMRT = true;

            if (path != null)
            {
                FSOEnvironment.ContentDir = "Content/";
                FSOEnvironment.GFXContentDir = "Content/" + (useDX ? "DX/" : "OGL/");
                FSOEnvironment.Linux = false;
                FSOEnvironment.DirectX = useDX;
                FSOEnvironment.GameThread = Thread.CurrentThread;
                //never chosen in Simitone: follow the language the installed game was set to.
                if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = GameInstall.InstalledLanguage();
                if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = 1;
                FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode = (FSO.Files.Formats.IFF.Chunks.STRLangCode)GlobalSettings.Default.LanguageCode;

                GlobalSettings.Default.StartupPath = path;
                GlobalSettings.Default.TS1HybridEnable = true;
                GlobalSettings.Default.TS1HybridPath = path;
                GlobalSettings.Default.ClientVersion = "0";
                GlobalSettings.Default.LightingMode = startup.LightingMode;
                GlobalSettings.Default.AntiAlias = aa ? 1 : 0;
                GlobalSettings.Default.ComplexShaders = startup.ComplexShaders;
                GlobalSettings.Default.EnableTransitions = startup.EnableTransitions;

                if (ide) new FSO.IDE.VolcanicStartProxy().InitVolcanic(args);

                var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
                //var globals = new TS1.Scripts.Dummy(); //make sure scripts assembly is loaded
                if (jit) assemblies.InitAOT();
                FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);

                var start = new GameStartProxy();
                start.Start(useDX);
            }
        }

        private static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var name = args.Name;
                if (name.StartsWith("FSO.Scripts"))
                {
                    return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == name);
                }
                else
                {
                    var assemblyPath = Path.Combine(MonogameLinker.AssemblyDir, args.Name.Substring(0, name.IndexOf(',')) + ".dll");
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    return assembly;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject;
            if (exception is OutOfMemoryException)
            {
                MessageBox.Show(e.ExceptionObject.ToString(), "Out of Memory! FreeSO needs to close.");
            }
            else
            {
                MessageBox.Show(e.ExceptionObject.ToString(), "A fatal error occured! Screenshot this dialog and post it on Discord.");
            }
        }

        public static Tuple<byte[], int, int> BitmapReader(Stream str)
        {
            Bitmap image = (Bitmap)Bitmap.FromStream(str);
            try
            {
                // Fix up the Image to match the expected format
                image = (Bitmap)image.RGBToBGR();

                var data = new byte[image.Width * image.Height * 4];

                BitmapData bitmapData = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (bitmapData.Stride != image.Width * 4)
                    throw new NotImplementedException();
                Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                image.UnlockBits(bitmapData);

                return new Tuple<byte[], int, int>(data, image.Width, image.Height);
            }
            finally
            {
                image.Dispose();
            }
        }

        // RGB to BGR convert Matrix
        private static float[][] rgbtobgr = new float[][]
          {
             new float[] {0, 0, 1, 0, 0},
             new float[] {0, 1, 0, 0, 0},
             new float[] {1, 0, 0, 0, 0},
             new float[] {0, 0, 0, 1, 0},
             new float[] {0, 0, 0, 0, 1}
          };


        internal static Image RGBToBGR(this Image bmp)
        {
            Image newBmp;
            if ((bmp.PixelFormat & System.Drawing.Imaging.PixelFormat.Indexed) != 0)
            {
                newBmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            else
            {
                // Need to clone so the call to Clear() below doesn't clear the source before trying to draw it to the target.
                newBmp = (Image)bmp.Clone();
            }

            try
            {
                System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();
                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(rgbtobgr);

                ia.SetColorMatrix(cm);
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, ia);
                }
            }
            finally
            {
                if (newBmp != bmp)
                {
                    bmp.Dispose();
                }
            }

            return newBmp;
        }
    }
#endif
}
