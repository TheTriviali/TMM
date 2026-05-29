using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class LoadoutDiffWindow : TmmWindow
    {
        private readonly BackendCore _core;
        private readonly string _gameKey;

        public LoadoutDiffWindow(BackendCore core, string gameKey, IList<string> loadoutNames)
        {
            InitializeComponent();
            _core = core;
            _gameKey = gameKey;

            cmbA.ItemsSource = loadoutNames;
            cmbB.ItemsSource = loadoutNames;
            if (loadoutNames.Count >= 2)
            {
                cmbA.SelectedIndex = 0;
                cmbB.SelectedIndex = 1;
            }
        }

        public sealed class DiffRow
        {
            public string ModName { get; set; } = "";
            public string StateA { get; set; } = "";
            public string StateB { get; set; } = "";
            public string Change { get; set; } = "";
        }

        private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbA.SelectedItem is not string a || cmbB.SelectedItem is not string b)
                return;

            if (a == b)
            {
                lvDiff.ItemsSource = null;
                txtSummary.Text = "Pick two different loadouts to see a diff.";
                return;
            }

            var loadoutA = await _core.ReadLoadoutAsync(_gameKey, a);
            var loadoutB = await _core.ReadLoadoutAsync(_gameKey, b);
            if (loadoutA == null || loadoutB == null)
            {
                txtSummary.Text = "Failed to read one or both loadouts.";
                return;
            }

            var rows = new List<DiffRow>();
            var allMods = loadoutA.ModStates.Keys.Union(loadoutB.ModStates.Keys).OrderBy(n => n);

            int adds = 0, removes = 0, orderChanges = 0;
            foreach (var name in allMods)
            {
                loadoutA.ModStates.TryGetValue(name, out var sA);
                loadoutB.ModStates.TryGetValue(name, out var sB);

                string stateA = sA is null ? "—" : (sA.IsEnabled ? $"on (#{sA.LoadOrder})" : "off");
                string stateB = sB is null ? "—" : (sB.IsEnabled ? $"on (#{sB.LoadOrder})" : "off");

                string change;
                if (sA is null && sB is not null)
                {
                    change = sB.IsEnabled ? "added (enabled)" : "added (disabled)";
                    adds++;
                }
                else if (sA is not null && sB is null)
                {
                    change = "removed";
                    removes++;
                }
                else if (sA!.IsEnabled != sB!.IsEnabled)
                {
                    change = sB.IsEnabled ? "enabled in B" : "disabled in B";
                }
                else if (sA.LoadOrder != sB.LoadOrder)
                {
                    change = $"order {sA.LoadOrder} → {sB.LoadOrder}";
                    orderChanges++;
                }
                else continue;

                rows.Add(new DiffRow { ModName = name, StateA = stateA, StateB = stateB, Change = change });
            }

            lvDiff.ItemsSource = rows;
            txtSummary.Text = $"{rows.Count} differences  ·  +{adds} added  ·  -{removes} removed  ·  {orderChanges} reordered";
        }
    }
}
