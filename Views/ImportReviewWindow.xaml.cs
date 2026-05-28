using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace TMM
{
    public partial class ImportReviewWindow : Window
    {
        public ObservableCollection<ModImportCandidate> Candidates { get; }

        public ImportReviewWindow(IEnumerable<ModImportCandidate> candidates)
        {
            Candidates = new ObservableCollection<ModImportCandidate>(candidates);
            InitializeComponent();
            DataContext = this;
        }

        public List<ModImportCandidate> GetSelectedCandidates() =>
            Candidates.Where(c => c.IsSelected).ToList();

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
