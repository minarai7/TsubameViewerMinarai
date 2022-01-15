﻿using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenFolderItemCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly SourceChoiceCommand _sourceChoiceCommand;
        private readonly AlbamCreateCommand _albamCreateCommand;

        public OpenFolderItemCommand(
            IMessenger messenger,
            FolderContainerTypeManager folderContainerTypeManager,
            SourceChoiceCommand sourceChoiceCommand,
            AlbamCreateCommand albamCreateCommand
            )
        {
            _messenger = messenger;
            _folderContainerTypeManager = folderContainerTypeManager;
            _sourceChoiceCommand = sourceChoiceCommand;
            _albamCreateCommand = albamCreateCommand;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type is StorageItemTypes.Image or StorageItemTypes.Archive or StorageItemTypes.ArchiveFolder or StorageItemTypes.Albam or StorageItemTypes.AlbamImage)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((item.Item as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                    }
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
                else if (item.Type == StorageItemTypes.AddFolder)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
                else if (item.Type == StorageItemTypes.AddAlbam)
                {
                    ((ICommand)_albamCreateCommand).Execute(null);
                }
            }
        }
    }
}
