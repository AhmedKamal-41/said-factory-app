using System.IO;
using System.Windows;

namespace FactoryApp
{
    public static class ThemeManager
    {
        public static event EventHandler? ThemeChanged;

        private const string ThemeFileName = "theme.txt";
        private const string DarkToken = "dark";
        private const string LightToken = "light";

        private static string ThemeFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactoryApp", ThemeFileName);

        public static bool IsDark { get; private set; }

        public static void Initialize()
        {
            try
            {
                var dir = Path.GetDirectoryName(ThemeFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }
            catch
            {
                // ignore; Apply still works with default
            }

            IsDark = ReadSavedIsDark();
            Apply(IsDark, persist: false);
        }

        public static void Toggle()
        {
            Apply(!IsDark, persist: true);
        }

        private static void Apply(bool dark, bool persist)
        {
            IsDark = dark;
            var app = Application.Current;
            if (app?.Resources.MergedDictionaries is not { } md)
                return;

            for (var i = md.Count - 1; i >= 0; i--)
            {
                var source = md[i].Source;
                if (source == null)
                    continue;
                var path = source.OriginalString.Replace('\\', '/');
                // Only swap palette dictionaries — do not remove Themes/SharedStyles.xaml
                if (path.EndsWith("/Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase))
                    md.RemoveAt(i);
            }

            var pack = dark
                ? "pack://application:,,,/FactoryApp;component/Themes/Dark.xaml"
                : "pack://application:,,,/FactoryApp;component/Themes/Light.xaml";
            md.Insert(0, new ResourceDictionary { Source = new Uri(pack, UriKind.Absolute) });

            ThemeChanged?.Invoke(null, EventArgs.Empty);

            if (persist)
                Save(IsDark);
        }

        private static bool ReadSavedIsDark()
        {
            try
            {
                if (!File.Exists(ThemeFilePath))
                    return false;
                var line = File.ReadAllText(ThemeFilePath).Trim();
                return string.Equals(line, DarkToken, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void Save(bool dark)
        {
            try
            {
                File.WriteAllText(ThemeFilePath, dark ? DarkToken : LightToken);
            }
            catch
            {
                // non-fatal
            }
        }
    }
}
