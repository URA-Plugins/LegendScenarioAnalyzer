using System.Drawing;
using System.Globalization;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TAttribute = Terminal.Gui.Drawing.Attribute;
using TColor = Terminal.Gui.Drawing.Color;
using UmamusumeResponseAnalyzer.TerminalGui;

namespace LegendScenarioAnalyzer;

internal static class LegendTrainingDisplayRenderer
{
    public static WorkspaceContent Render(LegendTrainingDisplayBuilder builder)
    {
        var snapshot = LegendDisplaySnapshot.Create(builder);
        return new WorkspaceContent(() => new LegendDashboardView(snapshot));
    }

    sealed record LegendDisplaySnapshot(
        int MainWidth,
        LegendPanelSnapshot[] Headers,
        LegendDisplayLine[] ImportantRows,
        LegendPanelSnapshot[] ScenarioPanels,
        LegendTrainingCardSnapshot[] TrainingCards,
        LegendSelectionCardSnapshot[] SelectionCards,
        LegendDisplayLine[] ExtraRows)
    {
        public static LegendDisplaySnapshot Create(LegendTrainingDisplayBuilder builder)
            => new(
                CommandInfoLayout.Current.MainSectionWidth,
                [.. builder.HeaderPanels.Select(LegendPanelSnapshot.Create)],
                [.. builder.ImportantRows.Lines.Select(Copy)],
                [.. builder.ScenarioPanels.Select(LegendPanelSnapshot.Create)],
                [.. builder.TrainingCards.Select(LegendTrainingCardSnapshot.Create)],
                [.. builder.SelectionCards.Select(LegendSelectionCardSnapshot.Create)],
                [.. builder.ExtraRows.Lines.Select(Copy)]);

        internal static LegendDisplayLine Copy(LegendDisplayLine line)
            => new([.. line.Segments], line.IsRule);
    }

    sealed record LegendPanelSnapshot(
        string Key,
        string Title,
        int Ratio,
        bool ShowHeader,
        LegendDisplayLine[] Lines)
    {
        public static LegendPanelSnapshot Create(LegendDisplayPanel panel)
            => new(
                panel.Key,
                panel.Title,
                panel.Ratio,
                panel.ShowHeader,
                [.. panel.Lines.Select(LegendDisplaySnapshot.Copy)]);
    }

    sealed record LegendTrainingCardSnapshot(
        int TrainIndex,
        LegendDisplayLine Title,
        bool Highlighted,
        LegendDisplayLine[] Rows)
    {
        public static LegendTrainingCardSnapshot Create(LegendTrainingCard card)
            => new(
                card.TrainIndex,
                LegendDisplaySnapshot.Copy(card.StyledTitle),
                card.Highlighted,
                [.. card.Rows.Lines.Select(LegendDisplaySnapshot.Copy)]);
    }

    sealed record LegendSelectionCardSnapshot(
        int BuffId,
        LegendBuffColor Color,
        string SelectionLabel,
        string Title,
        string Effect,
        int Rank,
        string[] Rows,
        bool Highlighted)
    {
        public static LegendSelectionCardSnapshot Create(LegendSelectionCard card)
            => new(
                card.BuffId,
                card.Color,
                card.SelectionLabel,
                card.Title,
                card.Effect,
                card.Rank,
                [.. card.Rows],
                card.Highlighted);
    }

    sealed class LegendDashboardView : View
    {
        const int HeaderHeight = 3;
        const int ImportantHeight = 5;
        const int ScenarioHeight = 3;
        const int MinimumTrainingHeight = 17;
        const int MainAreaY = HeaderHeight + ImportantHeight + ScenarioHeight;

        readonly int mainWidth;
        readonly int minimumContentWidth;
        readonly int minimumContentHeight;
        readonly View main;
        readonly FrameView extras;

        public LegendDashboardView(LegendDisplaySnapshot snapshot)
        {
            Id = "legend-root";
            Width = Dim.Fill();
            Height = Dim.Fill();
            CanFocus = true;
            TabStop = TabBehavior.TabGroup;
            ViewportSettings = ViewportSettingsFlags.HasScrollBars;

            mainWidth = snapshot.MainWidth;
            minimumContentWidth = mainWidth + (mainWidth + 3) / 4;
            var selectionRows = BuildSelectionRows(snapshot.SelectionCards, mainWidth);
            var mainAreaHeight = snapshot.TrainingCards.Length == 0
                ? Math.Max(1, selectionRows.Length)
                : Math.Max(
                    MinimumTrainingHeight,
                    snapshot.TrainingCards.Max(x => x.Rows.Length + 4));
            minimumContentHeight = MainAreaY + mainAreaHeight;

            var normal = GetAttributeForRole(VisualRole.Normal);
            var palette = new LegendPalette(normal);
            SetScheme(palette.BaseScheme);

            main = new View
            {
                Id = "legend-main",
                X = 0,
                Y = 0,
                Width = mainWidth,
                Height = Dim.Fill()
            };
            main.SetScheme(palette.BaseScheme);
            AddPanelFrames(snapshot.Headers, palette, y: 0, height: HeaderHeight, showTitles: false, "header");
            main.Add(CreateFrame(
                "legend-important",
                null,
                0,
                HeaderHeight,
                mainWidth,
                ImportantHeight,
                snapshot.ImportantRows,
                palette,
                wordWrap: true));
            AddPanelFrames(
                snapshot.ScenarioPanels,
                palette,
                HeaderHeight + ImportantHeight,
                ScenarioHeight,
                showTitles: true,
                "scenario");
            AddMainArea(snapshot.TrainingCards, selectionRows, palette, mainAreaHeight);

            extras = CreateFrame(
                "legend-extras",
                null,
                mainWidth,
                0,
                minimumContentWidth - mainWidth,
                Dim.Fill(),
                snapshot.ExtraRows,
                palette,
                wordWrap: true);
            Add(main, extras);
        }

        protected override void OnSubViewLayout(LayoutEventArgs args)
        {
            var visibleWidth = Math.Max(1, Viewport.Width);
            var visibleHeight = Math.Max(1, Viewport.Height);
            var contentWidth = Math.Max(visibleWidth, minimumContentWidth);
            var contentHeight = Math.Max(visibleHeight, minimumContentHeight);
            var contentSize = new Size(contentWidth, contentHeight);
            if (GetContentSize() != contentSize)
                SetContentSize(contentSize);

            main.Height = contentHeight;
            extras.Width = contentWidth - mainWidth;
            extras.Height = contentHeight;

            var maxX = Math.Max(0, contentWidth - visibleWidth);
            var maxY = Math.Max(0, contentHeight - visibleHeight);
            if (Viewport.X > maxX || Viewport.Y > maxY)
            {
                Viewport = new Rectangle(
                    Math.Min(Viewport.X, maxX),
                    Math.Min(Viewport.Y, maxY),
                    Viewport.Width,
                    Viewport.Height);
            }

            base.OnSubViewLayout(args);
        }

        void AddPanelFrames(
            IReadOnlyList<LegendPanelSnapshot> panels,
            LegendPalette palette,
            int y,
            int height,
            bool showTitles,
            string idPrefix)
        {
            if (panels.Count == 0)
                return;

            var x = 0;
            var ratioTotal = panels.Sum(panel => Math.Max(1, panel.Ratio));
            var ratioUsed = 0;
            for (var i = 0; i < panels.Count; i++)
            {
                ratioUsed += Math.Max(1, panels[i].Ratio);
                var nextX = i == panels.Count - 1 ? mainWidth : mainWidth * ratioUsed / ratioTotal;
                main.Add(CreateFrame(
                    $"legend-{idPrefix}:{panels[i].Key}",
                    showTitles && panels[i].ShowHeader ? panels[i].Title : null,
                    x,
                    y,
                    nextX - x,
                    height,
                    panels[i].Lines,
                    palette,
                    wordWrap: false));
                x = nextX;
            }
        }

        void AddMainArea(
            IReadOnlyList<LegendTrainingCardSnapshot> trainingCards,
            IReadOnlyList<LegendDisplayLine> selectionRows,
            LegendPalette palette,
            int mainAreaHeight)
        {
            if (trainingCards.Count != 0)
            {
                AddTrainingFrames(trainingCards, palette, mainAreaHeight);
                return;
            }

            main.Add(CreateTextView(
                selectionRows.Count == 0
                    ? [LegendDisplayLine.Plain("非训练阶段")]
                    : selectionRows,
                palette,
                wordWrap: false,
                x: 0,
                y: MainAreaY,
                width: mainWidth,
                height: mainAreaHeight,
                id: selectionRows.Count == 0 ? "legend-non-training" : "legend-selection"));
        }

        void AddTrainingFrames(
            IReadOnlyList<LegendTrainingCardSnapshot> cards,
            LegendPalette palette,
            int trainingHeight)
        {
            var cardWidth = mainWidth / 5;
            var innerWidth = cardWidth - 3;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var frame = new FrameView
                {
                    Id = $"legend-training:{card.TrainIndex}",
                    X = i * cardWidth,
                    Y = MainAreaY,
                    Width = cardWidth,
                    Height = trainingHeight
                };
                frame.SetScheme(palette.BaseScheme);
                if (card.Highlighted)
                {
                    frame.Border.GetOrCreateView().SetScheme(
                        new Scheme(palette.Attribute(LegendDisplayColor.LightGreen)));
                }

                var y = 0;
                var titleWidth = Math.Min(innerWidth, card.Title.Text.GetColumns());
                frame.Add(CreateTextView(
                    [card.Title],
                    palette,
                    wordWrap: false,
                    x: Pos.Align(Alignment.Center),
                    y: y++,
                    width: titleWidth,
                    height: 1));
                frame.Add(new Line { X = 1, Y = y++, Width = innerWidth });
                foreach (var row in card.Rows)
                {
                    if (row.IsRule)
                    {
                        frame.Add(new Line { X = 1, Y = y++, Width = innerWidth });
                        continue;
                    }

                    frame.Add(CreateTextView(
                        [row],
                        palette,
                        wordWrap: false,
                        x: 1,
                        y: y++,
                        width: innerWidth,
                        height: 1));
                }
                main.Add(frame);
            }
        }

        static FrameView CreateFrame(
            string id,
            string? title,
            Pos x,
            Pos y,
            Dim width,
            Dim height,
            IReadOnlyList<LegendDisplayLine> rows,
            LegendPalette palette,
            bool wordWrap)
        {
            var frame = new FrameView
            {
                Id = id,
                Title = title ?? string.Empty,
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
            frame.SetScheme(palette.BaseScheme);
            frame.Add(CreateTextView(
                rows,
                palette,
                wordWrap,
                x: 1,
                y: 0,
                width: Dim.Fill(1),
                height: Dim.Fill()));
            return frame;
        }

        static TextView CreateTextView(
            IReadOnlyList<LegendDisplayLine> rows,
            LegendPalette palette,
            bool wordWrap,
            Pos x,
            Pos y,
            Dim width,
            Dim height,
            string? id = null)
        {
            var text = new StyledTextView(palette)
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                ReadOnly = true,
                CanFocus = false,
                WordWrap = wordWrap
            };
            if (id is not null)
                text.Id = id;
            text.Load([.. rows.Select(row => ToCells(row, palette))]);
            return text;
        }

        sealed class StyledTextView : TextView
        {
            readonly TAttribute blackNormal;

            public StyledTextView(LegendPalette palette)
            {
                blackNormal = palette.Normal;
                SetScheme(palette.BaseScheme);
            }

            protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
                => SetAttribute(line[idxCol].Attribute ?? blackNormal);

            protected override void OnDrawReadOnlyColor(List<Cell> line, int idxCol, int idxRow)
                => SetAttribute(line[idxCol].Attribute ?? blackNormal);

            protected override void OnDrawUsedColor(List<Cell> line, int idxCol, int idxRow)
                => SetAttribute(line[idxCol].Attribute ?? blackNormal);
        }

        static List<Cell> ToCells(LegendDisplayLine line, LegendPalette palette)
        {
            var cells = new List<Cell>();
            foreach (var segment in line.Segments)
            {
                cells.AddRange(Cell.ToCellList(
                    segment.Text,
                    palette.Attribute(segment.Color, segment.Style)));
            }
            return cells;
        }

        static LegendDisplayLine[] BuildSelectionRows(
            IReadOnlyList<LegendSelectionCardSnapshot> cards,
            int mainWidth)
        {
            if (cards.Count == 0)
                return [];

            var entries = cards.Select(card => (Card: card, Parts: BuildSelectionParts(card))).ToArray();
            var iconWidth = ColumnWidth(entries.Select(x => x.Parts.Icon), 6);
            var labelWidth = ColumnWidth(entries.Select(x => x.Parts.Label), 13);
            var titleWidth = ColumnWidth(entries.Select(x => x.Parts.Title), 12);
            var effectWidth = ColumnWidth(entries.Select(x => x.Parts.Effect), 12);
            var scoreWidth = ColumnWidth(entries.Select(x => x.Parts.Score), 13);
            const int structuralWidth = 14;
            var adviceWidth = Math.Max(
                1,
                mainWidth - structuralWidth - iconWidth - labelWidth - titleWidth - effectWidth - scoreWidth);

            var rows = new List<LegendDisplayLine>();
            LegendBuffColor? previousColor = null;
            foreach (var entry in entries)
            {
                if (previousColor is not null && previousColor != entry.Card.Color)
                    rows.Add(LegendDisplayLine.Plain(string.Empty));

                var accent = LegendColors.AccentColor(entry.Card.Color);
                var rail = entry.Card.Highlighted
                    ? LegendDisplayColor.LightGreen
                    : LegendColors.BorderColor(entry.Card.Color);
                rows.Add(LegendDisplayLine.Styled(
                    new("│", rail),
                    new(" "),
                    new(
                        $"{PadDisplay(entry.Parts.Icon, iconWidth)} {PadDisplay(entry.Parts.Label, labelWidth)}",
                        accent),
                    new("  "),
                    new(PadDisplay(entry.Parts.Title, titleWidth), Style: LegendDisplayStyle.Bold),
                    new("  "),
                    new(PadDisplay(entry.Parts.Effect, effectWidth)),
                    new("  "),
                    new(PadDisplay(entry.Parts.Score, scoreWidth), LegendDisplayColor.Gray),
                    new("  "),
                    new(PadDisplay(TruncateDisplay(entry.Parts.Advice, adviceWidth), adviceWidth), LegendDisplayColor.Gray),
                    new("  "),
                    new("▶", accent)));
                previousColor = entry.Card.Color;
            }
            return [.. rows];
        }

        static SelectionLineParts BuildSelectionParts(LegendSelectionCardSnapshot card)
        {
            var score = string.Empty;
            var adviceRows = new List<string>();
            foreach (var row in card.Rows)
            {
                if (row == LegendDisplayLine.Rule.Text)
                    continue;

                var text = CompactSelectionInlineText(card, NormalizeInlineText(row));
                if (text.Length == 0)
                    continue;
                if (text.StartsWith("AI评分:", StringComparison.Ordinal))
                    score = text;
                else
                    adviceRows.Add(text);
            }

            return new(
                TruncateDisplay($"● ★{card.Rank}", 6),
                TruncateDisplay(card.SelectionLabel, 13),
                TruncateDisplay(card.Title, 12),
                TruncateDisplay(card.Effect, 12),
                TruncateDisplay(score, 13),
                string.Join(" | ", adviceRows));
        }

        static string CompactSelectionInlineText(LegendSelectionCardSnapshot card, string text)
        {
            var separatorIndex = text.IndexOfAny([':', '：']);
            if (separatorIndex < 0)
                return text;

            var key = text[..separatorIndex].Trim();
            var value = text[(separatorIndex + 1)..].Trim();
            if (key == "AI评分")
                return $"AI评分:{value}";
            if (key != "AI建议")
                return text;
            if (value.StartsWith(card.SelectionLabel, StringComparison.Ordinal))
                value = value[card.SelectionLabel.Length..].Trim();

            var nameStart = value.IndexOf('（');
            var nameEnd = nameStart < 0 ? -1 : value.IndexOf('）', nameStart + 1);
            if (nameStart >= 0 && nameEnd > nameStart)
            {
                var name = value[(nameStart + 1)..nameEnd].Trim();
                var suffix = value[(nameEnd + 1)..].Trim();
                value = suffix.Length == 0 ? name : $"{name} {suffix}";
            }
            return $"AI建议:{value}";
        }

        static string NormalizeInlineText(string text)
        {
            var builder = new StringBuilder(text.Length);
            var previousWhitespace = false;
            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    previousWhitespace = true;
                    continue;
                }
                if (previousWhitespace && builder.Length != 0)
                    builder.Append(' ');
                builder.Append(ch);
                previousWhitespace = false;
            }
            return builder.ToString();
        }

        static int ColumnWidth(IEnumerable<string> values, int maximum)
            => Math.Min(maximum, values.Select(x => x.GetColumns()).DefaultIfEmpty().Max());

        static string PadDisplay(string text, int width)
        {
            var truncated = TruncateDisplay(text, width);
            return $"{truncated}{new string(' ', Math.Max(0, width - truncated.GetColumns()))}";
        }

        static string TruncateDisplay(string text, int maxWidth)
        {
            if (maxWidth <= 0)
                return string.Empty;
            if (text.GetColumns() <= maxWidth)
                return text;

            var builder = new StringBuilder();
            var width = 0;
            var elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                var element = (string)elements.Current;
                var elementWidth = element.GetColumns();
                if (width + elementWidth > maxWidth - 1)
                    break;
                builder.Append(element);
                width += elementWidth;
            }
            return builder.Append('…').ToString();
        }

        sealed record SelectionLineParts(
            string Icon,
            string Label,
            string Title,
            string Effect,
            string Score,
            string Advice);
    }

    sealed class LegendPalette
    {
        readonly TAttribute normal;

        public LegendPalette(TAttribute normal)
        {
            this.normal = normal;
            Normal = new(normal.Foreground, TColor.Black, normal.Style);
            BaseScheme = new Scheme
            {
                Normal = Normal,
                ReadOnly = Normal,
                Focus = Normal
            };
        }

        public TAttribute Normal { get; }
        public Scheme BaseScheme { get; }

        public TAttribute Attribute(
            LegendDisplayColor color,
            LegendDisplayStyle style = LegendDisplayStyle.Normal)
        {
            if (color is LegendDisplayColor.Normal && style is LegendDisplayStyle.Normal)
                return Normal;

            var textStyle = style is LegendDisplayStyle.Bold
                ? normal.Style | TextStyle.Bold
                : normal.Style;
            return new(Foreground(color), TColor.Black, textStyle);
        }

        TColor Foreground(LegendDisplayColor color) => color switch
        {
            LegendDisplayColor.Cyan => new(StandardColor.BrightCyan),
            LegendDisplayColor.Green => new(StandardColor.Green),
            LegendDisplayColor.Yellow => new(StandardColor.BrightYellow),
            LegendDisplayColor.Red => new(StandardColor.BrightRed),
            LegendDisplayColor.DarkOrange => new(StandardColor.DarkOrange),
            LegendDisplayColor.Aqua => new(StandardColor.BrightCyan),
            LegendDisplayColor.Lime => new(StandardColor.BrightGreen),
            LegendDisplayColor.LightGreen => new(StandardColor.LightGreen),
            LegendDisplayColor.Blue => new(StandardColor.Blue),
            LegendDisplayColor.Gray => new(StandardColor.Gray),
            _ => normal.Foreground
        };
    }
}
