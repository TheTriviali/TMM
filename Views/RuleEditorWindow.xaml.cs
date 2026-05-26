using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    // ── ConditionViewModel ─────────────────────────────────────────────────────

    /// <summary>Observable wrapper around Condition for the RuleEditorWindow condition list.</summary>
    public class ConditionViewModel : INotifyPropertyChanged
    {
        private static readonly ConditionType[] AllTypes = (ConditionType[])Enum.GetValues(typeof(ConditionType));

        private ConditionType _type = ConditionType.FileExtension;
        private ConditionOperator _operator = ConditionOperator.Is;
        private string _value = string.Empty;
        private LogicOperator _logic = LogicOperator.AND;

        public IEnumerable<ConditionType> AvailableTypes => AllTypes;

        public ConditionType Type
        {
            get => _type;
            set
            {
                if (_type == value) return;
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableOperators));
                // Reset operator to first valid one for new type
                var ops = GetOperators(value);
                if (!ops.Contains(_operator))
                    Operator = ops.First();
            }
        }

        public IEnumerable<ConditionOperator> AvailableOperators => GetOperators(_type);

        public ConditionOperator Operator
        {
            get => _operator;
            set { if (_operator != value) { _operator = value; OnPropertyChanged(); } }
        }

        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(); } }
        }

        public LogicOperator Logic
        {
            get => _logic;
            set { if (_logic != value) { _logic = value; OnPropertyChanged(); } }
        }

        public IEnumerable<LogicOperator> AvailableLogic =>
            new[] { LogicOperator.AND, LogicOperator.OR };

        // Bound by wizard to hide logic selector on last item
        private bool _isLastCondition = true;
        public bool IsLastCondition
        {
            get => _isLastCondition;
            set { if (_isLastCondition != value) { _isLastCondition = value; OnPropertyChanged(); } }
        }

        public Condition ToModel() =>
            new() { Type = _type, Operator = _operator, Value = _value, Logic = _logic };

        public static ConditionViewModel FromModel(Condition c) =>
            new() { _type = c.Type, _operator = c.Operator, _value = c.Value, _logic = c.Logic };

        private static ConditionOperator[] GetOperators(ConditionType type) => type switch
        {
            ConditionType.FileExtension or ConditionType.FilenameMatches =>
                new[] { ConditionOperator.Is, ConditionOperator.IsNot, ConditionOperator.Contains, ConditionOperator.EndsWith, ConditionOperator.MatchesRegex },
            ConditionType.HasFolder =>
                new[] { ConditionOperator.Is, ConditionOperator.IsNot },
            ConditionType.FolderCount or ConditionType.FileCount =>
                new[] { ConditionOperator.Equals, ConditionOperator.GreaterThan, ConditionOperator.LessThan },
            ConditionType.PathContains =>
                new[] { ConditionOperator.Contains, ConditionOperator.DoesNotContain, ConditionOperator.StartsWith, ConditionOperator.EndsWith, ConditionOperator.MatchesRegex },
            _ => (ConditionOperator[])Enum.GetValues(typeof(ConditionOperator))
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // ── RuleEditorWindow ───────────────────────────────────────────────────────

    public partial class RuleEditorWindow : TmmWindow
    {
        public RoutingRule? Result { get; private set; }

        private readonly ObservableCollection<ConditionViewModel> _conditions = new();

        public RuleEditorWindow(RoutingRule? existing = null)
        {
            InitializeComponent();
            icConditions.ItemsSource = _conditions;
            _conditions.CollectionChanged += (_, _) => RefreshLastFlags();

            if (existing is not null)
            {
                txtTitle.Text = "Edit Routing Rule";
                txtRuleName.Text = existing.Name;
                txtTargetPath.Text = existing.TargetPath;
                sliderPriority.Value = existing.Priority;
                chkAllowConflict.IsChecked = existing.AllowConflict;
                chkIsDefault.IsChecked = existing.IsDefault;

                rbBiasLower.IsChecked  = existing.LoadOrderBias == LoadOrderBias.Lower;
                rbBiasNone.IsChecked   = existing.LoadOrderBias == LoadOrderBias.None;
                rbBiasHigher.IsChecked = existing.LoadOrderBias == LoadOrderBias.Higher;

                foreach (var c in existing.Conditions)
                    _conditions.Add(ConditionViewModel.FromModel(c));
            }

            UpdatePriorityLabel();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(((App)Application.Current).Core.Settings);
        }

        private void BtnAddCondition_Click(object sender, RoutedEventArgs e)
        {
            _conditions.Add(new ConditionViewModel());
        }

        private void BtnRemoveCondition_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ConditionViewModel vm)
                _conditions.Remove(vm);
        }

        private void RefreshLastFlags()
        {
            for (int i = 0; i < _conditions.Count; i++)
                _conditions[i].IsLastCondition = (i < _conditions.Count - 1);
        }

        private void SliderPriority_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
            => UpdatePriorityLabel();

        private void UpdatePriorityLabel()
        {
            if (txtPriorityValue is not null)
                txtPriorityValue.Text = ((int)sliderPriority.Value).ToString();
        }

        private void BtnSuggest_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string path)
                txtTargetPath.Text = path;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtRuleName.Text.Trim();
            string target = txtTargetPath.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                txtValidationMsg.Text = "Rule name is required.";
                txtRuleName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(target))
            {
                txtValidationMsg.Text = "Target directory is required.";
                txtTargetPath.Focus();
                return;
            }

            var bias = rbBiasLower.IsChecked == true ? LoadOrderBias.Lower
                     : rbBiasHigher.IsChecked == true ? LoadOrderBias.Higher
                     : LoadOrderBias.None;

            Result = new RoutingRule
            {
                Name         = name,
                Conditions   = _conditions.Select(c => c.ToModel()).ToList(),
                TargetPath   = target,
                Priority     = (int)sliderPriority.Value,
                AllowConflict = chkAllowConflict.IsChecked == true,
                LoadOrderBias = bias,
                IsDefault    = chkIsDefault.IsChecked == true,
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
