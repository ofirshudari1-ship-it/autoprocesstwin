using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AutoProcessTwin
{
    // ממיר Markdown פשוט (#, ##, -, **bold**) ל-FlowDocument בשביל תצוגת
    // דוחות התדרוך. לא פארסר Markdown מלא בכוונה - רק מה שה-briefing.js
    // בפועל מייצר. RTL כברירת מחדל כי הדוחות בעברית.
    public static class MarkdownView
    {
        public static FlowDocument Render(string markdown)
        {
            var doc = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = Theme.Get("TextBrush"),
                PagePadding = new Thickness(0),
            };

            if (string.IsNullOrEmpty(markdown))
            {
                doc.Blocks.Add(new Paragraph(new Run("אין תוכן להצגה.")));
                return doc;
            }

            foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (line.Length == 0) continue;

                if (line.StartsWith("## "))
                {
                    var p = new Paragraph(new Run(line.Substring(3)))
                    {
                        FontSize = 17,
                        FontWeight = FontWeights.Bold,
                        Foreground = Theme.Get("AccentBrush"),
                        Margin = new Thickness(0, 14, 0, 6),
                    };
                    doc.Blocks.Add(p);
                }
                else if (line.StartsWith("# "))
                {
                    var p = new Paragraph(new Run(line.Substring(2)))
                    {
                        FontSize = 21,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10),
                    };
                    doc.Blocks.Add(p);
                }
                else if (line.StartsWith("- "))
                {
                    var p = new Paragraph { Margin = new Thickness(0, 2, 18, 2) };
                    p.Inlines.Add(new Run("• "));
                    AddInlineBold(p, line.Substring(2));
                    doc.Blocks.Add(p);
                }
                else if (line.StartsWith("_") && line.EndsWith("_") && line.Length > 1)
                {
                    var p = new Paragraph(new Run(line.Trim('_')))
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = Theme.Get("TextMutedBrush"),
                        FontSize = 12,
                        Margin = new Thickness(0, 6, 0, 6),
                    };
                    doc.Blocks.Add(p);
                }
                else
                {
                    var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                    AddInlineBold(p, line);
                    doc.Blocks.Add(p);
                }
            }

            return doc;
        }

        // תמיכה מינימלית ב-**bold** בתוך שורה.
        private static void AddInlineBold(Paragraph p, string text)
        {
            var parts = text.Split(new[] { "**" }, System.StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var run = new Run(parts[i]);
                if (i % 2 == 1) run.FontWeight = FontWeights.Bold;
                p.Inlines.Add(run);
            }
        }
    }
}
