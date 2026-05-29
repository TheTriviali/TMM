using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TMM.Services;

namespace TMM
{
    public partial class ConflictResolverWindow : TmmWindow
    {
        private readonly string _gameDir;
        private readonly List<ConflictRow> _rows;

        /// <summary>After Apply, maps DestinationPath → chosen winner ModName.</summary>
        public Dictionary<string, string> WinnerOverrides { get; } = new();

        public ConflictResolverWindow(List<ConflictEntry> conflicts, string gameDir)
        {
            InitializeComponent();
            _gameDir = gameDir;

            _rows = conflicts.Select(c =>
            {
                int defaultIdx = c.Participants
                    .Select((p, i) => (p, i))
                    .OrderByDescending(t => t.p.LoadOrder)
                    .First().i;

                return new ConflictRow
                {
                    DestinationPath = c.DestinationPath,
                    RelativePath = Path.GetRelativePath(gameDir, c.DestinationPath),
                    Participants = c.Participants,
                    ChosenIndex = defaultIdx,
                    DefaultIndex = defaultIdx,
                };
            }).ToList();

            icConflicts.ItemsSource = _rows;
            txtHint.Text = $"{_rows.Count} conflicting destination(s). Highest load order is preselected; override below if you want a different mod to win.";
        }

        public sealed class ConflictRow
        {
            public string DestinationPath { get; set; } = "";
            public string RelativePath { get; set; } = "";
            public List<ConflictParticipant> Participants { get; set; } = new();
            public int ChosenIndex { get; set; }
            public int DefaultIndex { get; set; }

            public IEnumerable<string> ParticipantLabels =>
                Participants.Select((p, i) =>
                {
                    string suffix = i == DefaultIndex ? "  (default — highest load order)" : "";
                    return $"{p.ModName}  [load order {p.LoadOrder}]{suffix}";
                });

            public string ParticipantSummary =>
                $"{Participants.Count} mods write to this path.";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            WinnerOverrides.Clear();
            foreach (var row in _rows)
            {
                int idx = row.ChosenIndex;
                if (idx < 0 || idx >= row.Participants.Count) idx = row.DefaultIndex;
                WinnerOverrides[row.DestinationPath] = row.Participants[idx].ModName;
            }
            DialogResult = true;
            Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.ChosenIndex = row.DefaultIndex;
            icConflicts.ItemsSource = null;
            icConflicts.ItemsSource = _rows;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
