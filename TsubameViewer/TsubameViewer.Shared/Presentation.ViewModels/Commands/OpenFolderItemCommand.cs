﻿using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels.Commands
{
    public sealed class OpenFolderItemCommand : DelegateCommandBase
    {
        private readonly INavigationService _navigationService;

        public OpenFolderItemCommand(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == Windows.Storage.StorageItemTypes.File)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Views.ImageCollectionViewerPage), parameters, new DrillInNavigationTransitionInfo());
                }
                else if (item.Type == Windows.Storage.StorageItemTypes.Folder)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                }
            }
        }
    }
}
