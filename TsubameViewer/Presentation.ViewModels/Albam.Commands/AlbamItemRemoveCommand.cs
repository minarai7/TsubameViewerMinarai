﻿using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamItemRemoveCommand : ImageSourceCommandBase
    {
        private readonly AlbamRepository _albamRepository;

        public AlbamItemRemoveCommand(
            AlbamRepository albamRepository            
            )
        {
            _albamRepository = albamRepository;
        }

        protected override bool CanExecute(IImageSource imageSource)
        {
            return imageSource is AlbamItemImageSource;
        }

        protected override void Execute(IImageSource imageSource)
        {
            if (imageSource is AlbamItemImageSource albamItem)
            {
                _albamRepository.DeleteAlbamItem(albamItem.AlbamId, albamItem.Path, imageSource.GetAlbamItemType());
            }            
        }

        protected override void Execute(IEnumerable<IImageSource> imageSources)
        {
            base.Execute(imageSources.ToArray());
        }
    }
}
