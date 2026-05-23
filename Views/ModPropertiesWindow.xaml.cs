using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    public partial class ModPropertiesWindow : Window
    {
        public ModPropertiesWindow(ModItem mod)
        {
            InitializeComponent();

            txtName.Text = mod.Name;
            txtStatus.Text = mod.IsEnabled ? "Enabled" : "Disabled";
            txtOrder.Text = mod.LoadOrder.ToString();
            txtSource.Text = string.IsNullOrEmpty(mod.PackedFilePath)
                ? "Installed as Raw Folder"
                : mod.PackedFilePath;
            txtFolder.Text = mod.RawFolderPath;

            if (Directory.Exists(mod.RawFolderPath))
            {
                var rootNode = new TreeViewItem
                {
                    Header = mod.Name,
                    IsExpanded = true,
                    Foreground = Brushes.LightSkyBlue
                };
                treeFiles.Items.Add(rootNode);
                PopulateTree(mod.RawFolderPath, rootNode);
            }
            else
            {
                treeFiles.Items.Add(new TreeViewItem
                {
                    Header = "Directory not found. Mod may be deleted or moved.",
                    Foreground = Brushes.Red
                });
            }
        }

        private static void PopulateTree(string dir, TreeViewItem parentNode)
        {
            try
            {
                foreach (var d in Directory.GetDirectories(dir))
                {
                    var node = new TreeViewItem
                    {
                        Header = Path.GetFileName(d),
                        Foreground = Brushes.Wheat
                    };
                    parentNode.Items.Add(node);
                    PopulateTree(d, node);
                }

                foreach (var f in Directory.GetFiles(dir))
                {
                    parentNode.Items.Add(new TreeViewItem
                    {
                        Header = Path.GetFileName(f),
                        Foreground = Brushes.White
                    });
                }
            }
            catch { /* access denied - skip */ }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
