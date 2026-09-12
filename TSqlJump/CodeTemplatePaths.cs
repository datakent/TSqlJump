using System;
using System.IO;

namespace TSqlJump
{
    public static class CodeTemplatePaths
    {
        public static string RootDirectory
        {
            get
            {
                //C:\Users\{user}\AppData\Local\TSqlJump
                //%LocalAppData%\TSqlJump\Templates.toml
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "TSqlJump");
            }
        }

        public static string SnippetsFile
        {
            get
            {
                return Path.Combine(
                    RootDirectory,
                    "Templates.toml");
            }
        }

        public static string SettingsFile
        {
            get
            {
                return Path.Combine(
                    RootDirectory,
                    "Settings.toml");
            }
        }

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(RootDirectory);
        }
    }
}