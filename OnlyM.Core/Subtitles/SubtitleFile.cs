namespace OnlyM.Core.Subtitles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using HtmlAgilityPack;

    /// <summary>
    /// Simple SRT file parser
    /// </summary>
    internal sealed class SubtitleFile
    {
        private const string AssStartToken = "{\\";
        
        private readonly List<SubtitleEntry> _subtitles = new List<SubtitleEntry>();
        private int _index = -1;

        public SubtitleFile(string srtPath)
        {
            if (!Read(srtPath))
            {
                _subtitles = null;
            }
        }

        public int Count => _subtitles?.Count ?? 0;

        public void Reset()
        {
            _index = -1;
        }

        public SubtitleEntry GetNext()
        {
            if (Count > 0 && _index < Count - 2)
            {
                return _subtitles[++_index];
            }
            
            return null;
        }

        private bool Read(string srtPath)
        {
            var lines = File.ReadAllLines(srtPath);
            
            for (int n = 0; n < lines.Length; ++n)
            {
                var line = lines[n];
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                // first line in section is the subtitle number (1, 2, 3 etc)...
                if (!int.TryParse(line, out var number))
                {
                    return false;
                }

                if (n >= lines.Length - 1)
                {
                    return false;
                }
                
                line = lines[++n];

                // second line is the start and end timing of the subtitle...
                if (!SubtitleTiming.TryParse(line, out var timing))
                {
                    return false;
                }

                if (n >= lines.Length - 1)
                {
                    return false;
                }

                line = lines[++n];

                var text = new List<string>();
                while (!string.IsNullOrEmpty(line) && n < lines.Length - 1)
                {
                    text.Add(line.Trim());
                    line = lines[++n];
                }
                
                var entry = new SubtitleEntry
                {
                    Number = number,
                    Timing = timing,
                    Text = StripHtml(text)
                };

                _subtitles.Add(entry);
            }

            return true;
        }

        private string StripHtml(IReadOnlyCollection<string> lines)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(string.Join(Environment.NewLine, lines));
            return StripASSCodes(doc.DocumentNode.InnerText);
        }

        private string StripASSCodes(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (!s.Contains(AssStartToken))
            {
                return s;
            }

            var sb = new StringBuilder();

            int assCodeLevel = 0;
            for (int n = 0; n < s.Length; ++n)
            {
                var ch = s[n];

                if (ch == '{' && n < s.Length - 1 && s[n + 1] == '\\')
                {
                    ++assCodeLevel;
                }
                else if (assCodeLevel > 0 && ch == '}')
                {
                    --assCodeLevel;
                }
                else if (assCodeLevel == 0)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Trim();
        }
    }
}
