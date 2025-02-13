﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.UseCase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TsubameViewer.Presentation.ViewModels.SourceFolders;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using static TsubameViewer.Models.Domain.FolderItemListing.ThumbnailManager;
    using static TsubameViewer.Presentation.ViewModels.ImageListupPageViewModel;
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly AlbamRepository _albamRepository;

        public IImageSource Item { get; }
        public SelectionContext Selection { get; }
        public string Name { get; }

        
        public string Path { get; }

        public DateTimeOffset DateCreated { get; }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            set { SetProperty(ref _image, value); }
        }

        private float? _ImageAspectRatioWH;
        public float? ImageAspectRatioWH
        {
            get { return _ImageAspectRatioWH; }
            set { SetProperty(ref _ImageAspectRatioWH, value); }
        }


        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }

        public StorageItemTypes Type { get; }

        private double _ReadParcentage;
        public double ReadParcentage
        {
            get { return _ReadParcentage; }
            set { SetProperty(ref _ReadParcentage, value); }
        }

        public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;


        public StorageItemViewModel(string name, StorageItemTypes storageItemTypes)         
        {
            Name = name;
            Type = storageItemTypes;
        }

        public StorageItemViewModel(IImageSource item, IMessenger messenger, SourceStorageItemsRepository sourceStorageItemsRepository, BookmarkManager bookmarkManager, AlbamRepository albamRepository, SelectionContext selectionContext = null)
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;
            _albamRepository = albamRepository;
            Selection = selectionContext;            
            Item = item;
            _messenger = messenger;
            DateCreated = Item.DateCreated;
            
            Name = Item.Name;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            Path = item.Path;

            _ImageAspectRatioWH = Item.GetThumbnailSize()?.RatioWH;

            UpdateLastReadPosition();
            _isFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, item.Path);
        
        }


        private bool _isRequestImageLoading = false;
        private bool _isRequireLoadImageWhenRestored = false;
        public void StopImageLoading()
        {
            _isRequestImageLoading = false;
        }

        private readonly static Models.Infrastructure.AsyncLock _asyncLock = new (4);

        bool _isInitialized = false;
        public async void Initialize(CancellationToken ct)
        {
            // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
            // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
            _isRequestImageLoading = true;

            try
            {
                using var _ = await _asyncLock.LockAsync(ct);

                if (_isInitialized) { return; }
                if (_disposed) { return; }
                if (Item == null) { return; }
                if (_isRequestImageLoading is false) { return; }

                using (var stream = await Task.Run(async () => await Item.GetThumbnailImageStreamAsync(ct)))
                {
                    if (stream is null || stream.Size == 0) { return; }
                    
                    stream.Seek(0);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    //bitmapImage.DecodePixelHeight = Models.Domain.FolderItemListing.ListingImageConstants.LargeFileThumbnailImageHeight;
                    await bitmapImage.SetSourceAsync(stream).AsTask(ct);
                    Image = bitmapImage;
                }

                ImageAspectRatioWH ??= Item.GetThumbnailSize()?.RatioWH;

                _isRequireLoadImageWhenRestored = false;
                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                _isRequireLoadImageWhenRestored = true;
                _isInitialized = false;
            }                       
            catch (NotSupportedImageFormatException ex) 
            {
                // 0xC00D5212
                // "コンテンツをエンコードまたはデコードするための適切な変換が見つかりませんでした。"
                _isRequireLoadImageWhenRestored = true;
                _isInitialized = false;
                _messenger.Send<RequireInstallImageCodecExtensionMessage>(new (ex.FileType));
                throw;
            }
        }

        public void UpdateLastReadPosition()
        {
            var parcentage = _bookmarkManager.GetBookmarkLastReadPositionInNormalized(Path);
            ReadParcentage = parcentage >= 0.90f ? 1.0 : parcentage;
        }

        public void RestoreThumbnailLoadingTask(CancellationToken ct)
        {
            IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Path);

            if (_isRequireLoadImageWhenRestored && Image == null)
            {
                Initialize(ct);
            }
        }

        public void ThumbnailChanged()
        {
            Image = null;
            _isInitialized = false;
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            
            _disposed = true;
            (Item as IDisposable)?.Dispose();
            _image = null;
        }
        bool _disposed;
    }
}
