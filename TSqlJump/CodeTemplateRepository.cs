using System;
using System.Collections.Generic;
using System.IO;

namespace TSqlJump
{
    public sealed class CodeTemplateRepository
    {
        private readonly string filePath;

        public CodeTemplateRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "CodeTemplate file paths cannot be empty.",
                    nameof(filePath));

            this.filePath = filePath;
        }

        public List<CodeTemplateSnippet> GetAll()
        {
            return TomlParser.Parse(filePath);
        }

        public CodeTemplateSnippet GetByShortName(string shortName)
        {
            if (string.IsNullOrWhiteSpace(shortName))
                return null;

            List<CodeTemplateSnippet> snippets = GetAll();

            foreach (CodeTemplateSnippet snippet in snippets)
            {
                if (string.Equals(
                    snippet.ShortName,
                    shortName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return snippet;
                }
            }

            return null;
        }
    }
}