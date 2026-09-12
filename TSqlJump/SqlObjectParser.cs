/*
    | SQL                    | Database | Schema | Object  |
    | ---------------------- | -------- | ------ | ------- |
    | `STOKLAR`              | —        | —      | STOKLAR |
    | `dbo.STOKLAR`          | —        | dbo    | STOKLAR |
    | `DB..STOKLAR`          | DB       | —      | STOKLAR |
    | `DB.dbo.STOKLAR`       | DB       | dbo    | STOKLAR |
    | `[STOKLAR]`            | —        | —      | STOKLAR |
    | `[dbo].[STOKLAR]`      | —        | dbo    | STOKLAR |
    | `[DB]..[STOKLAR]`      | DB       | —      | STOKLAR |
    | `[DB].[dbo].[STOKLAR]` | DB       | dbo    | STOKLAR |  
*/

using System;
using System.Collections.Generic;

namespace TSqlJump
{
    public static class SqlObjectParser
    {
        public static SqlObjectReference Parse(
            string line,
            int cursorColumn)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            int cursor = cursorColumn - 1;

            if (cursor < 0)
                cursor = 0;

            if (cursor >= line.Length)
                cursor = line.Length - 1;

            if (cursor < 0)
                return null;

            // Find the identifier part containing the cursor.
            int start;
            int end;

            if (line[cursor] == '[')
            {
                start = cursor;

                end = line.IndexOf(']', cursor + 1);

                if (end < 0)
                    return null;

                end++;
            }
            else
            {
                // Cursor may be somewhere inside [identifier].
                int bracketStart = line.LastIndexOf(
                    '[',
                    cursor);

                int bracketEnd = line.IndexOf(
                    ']',
                    cursor);

                if (bracketStart >= 0 &&
                    bracketEnd >= 0 &&
                    bracketStart < cursor &&
                    cursor <= bracketEnd)
                {
                    start = bracketStart;
                    end = bracketEnd + 1;
                }
                else
                {
                    start = cursor;
                    end = cursor;

                    while (start > 0 &&
                           IsIdentifierCharacter(line[start - 1]))
                    {
                        start--;
                    }

                    while (end < line.Length &&
                           IsIdentifierCharacter(line[end]))
                    {
                        end++;
                    }
                }
            }

            if (start >= end)
                return null;

            // Expand to the left through qualified-name parts.
            int left = start;

            while (true)
            {
                int dot = FindDotLeft(line, left);

                if (dot < 0)
                    break;

                // Handle DATABASE..OBJECT.
                if (dot > 0 &&
                    line[dot - 1] == '.')
                {
                    int secondDot = dot - 1;

                    int partStart =
                        FindIdentifierStart(line, secondDot);

                    if (partStart < secondDot)
                    {
                        left = partStart;
                        continue;
                    }

                    break;
                }

                int previousStart =
                    FindIdentifierStart(line, dot);

                if (previousStart >= dot)
                    break;

                left = previousStart;
            }

            // Expand to the right through qualified-name parts.
            int right = end;

            while (true)
            {
                int dot = FindDotRight(line, right);

                if (dot < 0)
                    break;

                // Skip consecutive dots in DATABASE..OBJECT.
                int nextStart = dot + 1;

                while (nextStart < line.Length &&
                       char.IsWhiteSpace(line[nextStart]))
                {
                    nextStart++;
                }

                if (nextStart < line.Length &&
                    line[nextStart] == '.')
                {
                    nextStart++;

                    while (nextStart < line.Length &&
                           char.IsWhiteSpace(line[nextStart]))
                    {
                        nextStart++;
                    }
                }

                int nextEnd =
                    FindIdentifierEnd(line, nextStart);

                if (nextEnd <= nextStart)
                    break;

                right = nextEnd;
            }

            string qualifiedName =
                line.Substring(
                    left,
                    right - left);

            return ParseQualifiedName(qualifiedName);
        }

        private static SqlObjectReference ParseQualifiedName(
            string value)
        {
            List<string> parts =
                SplitQualifiedName(value);

            if (parts.Count == 0)
                return null;

            var result =
                new SqlObjectReference();

            if (parts.Count == 1)
            {
                result.ObjectName = parts[0];
            }
            else if (parts.Count == 2)
            {
                result.Schema = parts[0];
                result.ObjectName = parts[1];
            }
            else if (parts.Count == 3)
            {
                result.Database = parts[0];
                result.Schema = parts[1];
                result.ObjectName = parts[2];
            }

            return result;
        }

        private static List<string> SplitQualifiedName(
            string value)
        {
            var parts =
                new List<string>();

            int start = 0;
            bool insideBracket = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '[')
                {
                    insideBracket = true;
                }
                else if (c == ']')
                {
                    insideBracket = false;
                }
                else if (c == '.' && !insideBracket)
                {
                    parts.Add(
                        Unquote(
                            value.Substring(
                                start,
                                i - start)));

                    start = i + 1;
                }
            }

            parts.Add(
                Unquote(
                    value.Substring(
                        start)));

            // DATABASE..OBJECT
            if (parts.Count == 3 &&
                string.IsNullOrEmpty(parts[1]))
            {
                parts[1] = null;
            }

            return parts;
        }

        private static int FindDotLeft(
            string line,
            int position)
        {
            int i = position - 1;

            while (i >= 0 &&
                   char.IsWhiteSpace(line[i]))
            {
                i--;
            }

            if (i >= 0 &&
                line[i] == '.')
            {
                return i;
            }

            return -1;
        }

        private static int FindDotRight(
            string line,
            int position)
        {
            int i = position;

            while (i < line.Length &&
                   char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            if (i < line.Length &&
                line[i] == '.')
            {
                return i;
            }

            return -1;
        }

        private static int FindIdentifierStart(
            string line,
            int position)
        {
            int i = position - 1;

            while (i >= 0 &&
                   char.IsWhiteSpace(line[i]))
            {
                i--;
            }

            if (i < 0)
                return position;

            if (line[i] == ']')
            {
                int bracketStart =
                    line.LastIndexOf(
                        '[',
                        i);

                return bracketStart >= 0
                    ? bracketStart
                    : position;
            }

            while (i >= 0 &&
                   IsIdentifierCharacter(line[i]))
            {
                i--;
            }

            return i + 1;
        }

        private static int FindIdentifierEnd(
            string line,
            int position)
        {
            int i = position;

            while (i < line.Length &&
                   char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            if (i < line.Length &&
                line[i] == '[')
            {
                int bracketEnd =
                    line.IndexOf(
                        ']',
                        i + 1);

                return bracketEnd >= 0
                    ? bracketEnd + 1
                    : i;
            }

            while (i < line.Length &&
                   IsIdentifierCharacter(line[i]))
            {
                i++;
            }

            return i;
        }

        private static bool IsIdentifierCharacter(char c)
        {
            return char.IsLetterOrDigit(c)
                   || c == '_'
                   || c == '$'
                   || c == '#'
                   || c == '@';
        }

        private static string Unquote(string value)
        {
            value = value.Trim();

            if (value.Length >= 2 &&
                value[0] == '[' &&
                value[value.Length - 1] == ']')
            {
                return value.Substring(
                    1,
                    value.Length - 2);
            }

            return string.IsNullOrEmpty(value)
                ? null
                : value;
        }
    }
}