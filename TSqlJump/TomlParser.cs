using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TSqlJump
{
    public static class TomlParser
    {
        public static List<CodeTemplateSnippet> Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    "TSqlJump TOML file not found.",
                    filePath);

            string[] lines = File.ReadAllLines(
                filePath,
                Encoding.UTF8);

            var snippets = new List<CodeTemplateSnippet>();

            CodeTemplateSnippet current = null;

            bool readingTSql = false;
            var tsqlBuilder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // -------------------------------------------------
                // Çok satırlı TSQL okunuyor
                // -------------------------------------------------
                if (readingTSql)
                {
                    int closingIndex = line.IndexOf("\"\"\"");

                    if (closingIndex >= 0)
                    {
                        string content =
                            line.Substring(0, closingIndex);

                        tsqlBuilder.Append(content);

                        current.TSql =
                            tsqlBuilder.ToString();

                        readingTSql = false;

                        continue;
                    }

                    tsqlBuilder.AppendLine(line);
                    continue;
                }

                string trimmed = line.Trim();

                // Boş satır
                if (trimmed.Length == 0)
                    continue;

                // Yorum
                if (trimmed.StartsWith("#"))
                    continue;

                // -------------------------------------------------
                // Yeni snippet
                // -------------------------------------------------
                if (trimmed == "[[snippet]]")
                {
                    if (current != null)
                    {
                        ValidateSnippet(
                            current,
                            snippets,
                            i);

                        snippets.Add(current);
                    }

                    current = new CodeTemplateSnippet();

                    continue;
                }

                if (current == null)
                {
                    throw new FormatException(
                        "Data was found before the snippet definition was discovered. " +
                        "Row: " + (i + 1));
                }

                // -------------------------------------------------
                // Alan ayrıştırma
                // -------------------------------------------------
                int equalIndex =
                    trimmed.IndexOf('=');

                if (equalIndex <= 0)
                {
                    throw new FormatException(
                        "Invalid TOML line. " +
                        "Row: " + (i + 1));
                }

                string key =
                    trimmed.Substring(
                        0,
                        equalIndex)
                    .Trim();

                string value =
                    trimmed.Substring(
                        equalIndex + 1)
                    .Trim();

                // -------------------------------------------------
                // TSQL başlangıcı
                // -------------------------------------------------
                if (key == "tsql")
                {
                    if (!value.StartsWith("\"\"\""))
                    {
                        throw new FormatException(
                            "The SQL field must start with \"\"\" " +
                            "Row: " + (i + 1));
                    }

                    string remainder =
                        value.Substring(3);

                    int closingIndex =
                        remainder.IndexOf("\"\"\"");

                    // Açılış ve kapanış aynı satırda
                    if (closingIndex >= 0)
                    {
                        current.TSql =
                            remainder.Substring(
                                0,
                                closingIndex);

                        continue;
                    }

                    // Multiline moda geç
                    readingTSql = true;

                    tsqlBuilder.Clear();

                    // Açılış satırından sonra içerik varsa ekle
                    if (remainder.Length > 0)
                        tsqlBuilder.AppendLine(remainder);

                    continue;
                }

                // -------------------------------------------------
                // Normal alanlar
                // -------------------------------------------------
                switch (key)
                {
                    case "name":
                        current.Name =
                            ParseString(
                                value,
                                i + 1);
                        break;

                    case "shortName":
                        current.ShortName =
                            ParseString(
                                value,
                                i + 1);
                        break;

                    case "shortcut":
                        current.Shortcut =
                            ParseString(
                                value,
                                i + 1);
                        break;

                    default:
                        throw new FormatException(
                            "Bilinmeyen alan: '" +
                            key +
                            "'. Satır: " +
                            (i + 1));
                }
            }

            // -----------------------------------------------------
            // Dosya multiline TSQL içerisinde bittiyse
            // -----------------------------------------------------
            if (readingTSql)
            {
                throw new FormatException(
                    "The SQL field is not closed. \"\"\" missing.");
            }

            // -----------------------------------------------------
            // Son snippet
            // -----------------------------------------------------
            if (current != null)
            {
                ValidateSnippet(
                    current,
                    snippets,
                    lines.Length);

                snippets.Add(current);
            }

            return snippets;
        }

        private static string ParseString(
            string value,
            int lineNumber)
        {
            if (value.Length < 2 ||
                value[0] != '"' ||
                value[value.Length - 1] != '"')
            {
                throw new FormatException(
                    "Invalid string value. " +
                    "Row: " + lineNumber);
            }

            return value.Substring(
                1,
                value.Length - 2);
        }

        private static void ValidateSnippet(
            CodeTemplateSnippet snippet,
            List<CodeTemplateSnippet> snippets,
            int lineNumber)
        {
            if (string.IsNullOrWhiteSpace(
                snippet.Name))
            {
                throw new FormatException(
                    "The 'name' field is required for the snippet. " +
                    "Row: " + lineNumber);
            }

            if (string.IsNullOrWhiteSpace(
                snippet.ShortName))
            {
                throw new FormatException(
                    "The 'shortName' field is required for the snippet. " +
                    "Row: " + lineNumber);
            }

            if (snippet.TSql == null)
                snippet.TSql = string.Empty;

            foreach (CodeTemplateSnippet item in snippets)
            {
                if (string.Equals(
                    item.ShortName,
                    snippet.ShortName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException(
                        "The same shortName has been used multiple times: '" +
                        snippet.ShortName +
                        "'");
                }
            }
        }
    }
}