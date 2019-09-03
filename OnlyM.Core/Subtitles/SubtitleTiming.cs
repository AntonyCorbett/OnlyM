namespace OnlyM.Core.Subtitles
{
    using System;

    public class SubtitleTiming
    {
        public TimeSpan Start { get; set; }

        public TimeSpan End { get; set; }

        public static bool TryParse(string line, out SubtitleTiming value)
        {
            value = null;

            var tokens = line.Replace("-->", "|").Replace(",", ".").Split('|');
            if (tokens.Length != 2)
            {
                return false;
            }

            if (!TimeSpan.TryParse(tokens[0], out var start))
            {
                return false;
            }

            if (!TimeSpan.TryParse(tokens[1], out var end))
            {
                return false;
            }

            if (end < start)
            {
                return false;
            }

            value = new SubtitleTiming
            {
                Start = start,
                End = end,
            };

            return true;
        }
    }
}
