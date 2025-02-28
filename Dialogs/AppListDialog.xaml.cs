using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using WinDurango.UI.Utils;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace WinDurango.UI.Dialogs
{
    public sealed partial class AppListDialog
    {
        public readonly List<Package> Packages;
        
        public AppListDialog(List<Package> packages, bool multiSelect = false)
        {
            Packages = packages;

            this.DataContext = this;
            this.InitializeComponent();

            if (multiSelect)
                appListView.SelectionMode = ListViewSelectionMode.Multiple;
            appListView.MaxHeight = App.MainWindow.Bounds.Height * 0.65;

        }

        private void AppListView_Loaded(object sender, RoutedEventArgs e)
        {
            appListView.MinWidth = Math.Max(App.MainWindow.Bounds.Width / 2, 500);
            appListView.MaxWidth = Math.Max(App.MainWindow.Bounds.Width / 2, 500);

            ListView listView = (ListView)sender;

            foreach (Package package in Packages)
            {
                ListViewItem item = new() { MinWidth = 200 };
                StackPanel stackPanel = new() { Orientation = Orientation.Horizontal };
                
                Uri pkLogo = null;

                // NOTE: DO NOT TOUCH THIS MAGICAL SHIT 
                // it throws massive error if the image is invalid somehow or whatever...
                try
                {
                    pkLogo = package.Logo;
                }
                catch (Exception ex)
                {
                    Logger.WriteError($"pkg.Logo threw {ex.GetType()} for {package.Id.FamilyName}");
                    Logger.WriteException(ex);
                }
                
                Image packageLogo = new()
                {
                    Width = 64,
                    Height = 64,
                    Margin = new Thickness(5),
                    Source = new BitmapImage(pkLogo ?? new Uri("ms-appx:///Assets/no_img64.png"))
                };
                
                StackPanel packageInfo = new()
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(5)
                };

                string displayName;
                try
                {
                    displayName = package.DisplayName;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    displayName = package.Id.Name;
                }

                TextBlock packageName = new()
                {
                    Text = displayName ?? "Unknown",
                    FontWeight = FontWeights.Bold
                };
                
                TextBlock publisherName = new()
                {
                    Text = package.PublisherDisplayName ?? "Unknown"
                };

                packageInfo.Children.Add(packageName);
                packageInfo.Children.Add(publisherName);

                stackPanel.Children.Add(packageLogo);
                stackPanel.Children.Add(packageInfo);

                item.Content = stackPanel;
                item.Tag = package;

                listView.Items.Add(item);
            }
        }

        private void AddToAppList(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            foreach (ListViewItem listViewItem in appListView.SelectedItems)
            {
                var package = listViewItem.Tag as Package;
                if (package != null && package?.Id?.FamilyName != null)
                {
                    if (App.InstalledPackages.GetPackage(package.Id.FamilyName) == null)
                        App.InstalledPackages.AddPackage(package);
                }
            }

            _ = App.MainWindow.AppsListPage.InitAppListAsync();
        }

        private void HideDialog(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            this.Hide();
        }
    }
}
