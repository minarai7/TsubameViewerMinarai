﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Uno;
using Uno.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SourceStorageItemsPage : Page
    {
        public SourceStorageItemsPage()
        {
            this.InitializeComponent();

            this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as SourceStorageItemsPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private SourceStorageItemsPageViewModel _vm { get; set; }

        private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is StorageItemViewModel itemVM && itemVM.IsSourceStorageItem is false && itemVM.Name != null)
            {
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM.Name, TextWrapping = TextWrapping.Wrap } });
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await Task.Delay(500);

            var settings = new Models.Domain.FolderItemListing.FolderListingSettings();
            if (settings.IsForceEnableXYNavigation
                || Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                )
            {
                if (FoldersAdaptiveGridView.Items.Any())
                {
                    var firstItem = FoldersAdaptiveGridView.Items.First();
                    var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;
                    if (itemContainer != null)
                    {
                        itemContainer.Focus(FocusState.Keyboard);
                    }
                }
            }
        }
    }
}
