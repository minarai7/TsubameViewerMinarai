﻿using Microsoft.Toolkit.Uwp.UI;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using Uno.Disposables;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    

    public sealed class FolderListupPageViewModel : ViewModelBase, IDestructible
    {
        // Note: FolderListupPage内でフォルダ階層のコントロールをしている
        // フォルダ階層の移動はNavigationServiceを通さずにページ内で完結してる
        // 

        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        public OpenPageCommand OpenPageCommand { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public AltOpenFolderItemCommand AltOpenFolderItemCommand { get; }

        public ObservableCollection<StorageItemViewModel> FolderItems { get; }
        public ObservableCollection<StorageItemViewModel> ArchiveFileItems { get; }
        public ObservableCollection<StorageItemViewModel> ImageFileItems { get; }

        public AdvancedCollectionView FileItemsView { get; }

        private bool _HasFileItem;
        public bool HasFileItem
        {
            get { return _HasFileItem; }
            set { SetProperty(ref _HasFileItem, value); }
        }


        public FolderItemsGroupBase[] Groups { get; }

        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

        static FastAsyncLock _NavigationLock = new FastAsyncLock();

        IReadOnlyReactiveProperty<QueryOptions> _currentQueryOptions;

        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private IStorageItem _currentItem;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        bool _isCompleteEnumeration = false;

        private string _DisplayCurrentPath;
        public string DisplayCurrentPath
        {
            get { return _DisplayCurrentPath; }
            set { SetProperty(ref _DisplayCurrentPath, value); }
        }

        public ReactiveProperty<FileDisplayMode> FileDisplayMode { get; }
        public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
        {
            Models.Domain.FolderItemListing.FileDisplayMode.Large,
            Models.Domain.FolderItemListing.FileDisplayMode.Midium,
            Models.Domain.FolderItemListing.FileDisplayMode.Small,
        };

        public string FoldersManagementPageName => nameof(Views.SourceStorageItemsPage);


        static bool _LastIsImageFileThumbnailEnabled;
        static bool _LastIsArchiveFileThumbnailEnabled;
        static bool _LastIsFolderThumbnailEnabled;

        public FolderListupPageViewModel(
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            OpenPageCommand openPageCommand,
            OpenFolderItemCommand openFolderItemCommand,
            AltOpenFolderItemCommand altOpenFolderItemCommand
            )
        {
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
            OpenPageCommand = openPageCommand;
            OpenFolderItemCommand = openFolderItemCommand;
            AltOpenFolderItemCommand = altOpenFolderItemCommand;
            FolderItems = new ObservableCollection<StorageItemViewModel>();
            ArchiveFileItems = new ObservableCollection<StorageItemViewModel>();
            ImageFileItems = new ObservableCollection<StorageItemViewModel>();

            FileItemsView = new AdvancedCollectionView(ImageFileItems);
            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending);

            Groups = new FolderItemsGroupBase[]
            {
                new FolderFolderItemsGroup()
                {
                    Items = FolderItems,
                },
                new FileFolderItemsGroup()
                { 
                    Items = ArchiveFileItems
                }
            };


            FileDisplayMode = _folderListingSettings.ToReactivePropertyAsSynchronized(x => x.FileDisplayMode);

            /*
            _currentQueryOptions = Observable.CombineLatest(
                SelectedFolderViewFirstSort,
                (queryType, sort) => (queryType, sort)
                )
                .Select(_ =>
                {
                    var options = new QueryOptions();
                    options.FolderDepth = FolderDepth.Shallow;
                    options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.ImageProperties, Enumerable.Empty<string>());
                    return options;
                })
                .ToReadOnlyReactivePropertySlim();
                */

        }


        void IDestructible.Destroy()
        {
            SelectedFileSortType.Dispose();
        }





        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource?.Cancel();
            _leavePageCancellationTokenSource?.Dispose();
            _leavePageCancellationTokenSource = null;

            ImageFileItems.Reverse().ForEach(x => x.StopImageLoading());
            ArchiveFileItems.Reverse().ForEach(x => x.StopImageLoading());
            FolderItems.Reverse().ForEach(x => x.StopImageLoading());

            _LastIsImageFileThumbnailEnabled = _folderListingSettings.IsImageFileThumbnailEnabled;
            _LastIsArchiveFileThumbnailEnabled = _folderListingSettings.IsArchiveFileThumbnailEnabled;
            _LastIsFolderThumbnailEnabled = _folderListingSettings.IsFolderThumbnailEnabled;

            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            PrimaryWindowCoreLayout.CurrentNavigationParameters = parameters;

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationService = parameters.GetNavigationService();            

            NowProcessing = true;
            try
            {
                // Note: ファイル表示用のItemsRepeaterのItemTemplateが
                // VisualStateによって変更されるのを待つ
                await Task.Delay(50);

                var mode = parameters.GetNavigationMode();

                _leavePageCancellationTokenSource = new CancellationTokenSource();

                if (mode == NavigationMode.New
                    || mode == NavigationMode.Forward
                    || mode == NavigationMode.Back
                    )
                {
                    HasFileItem = false;

                    using (await _NavigationLock.LockAsync(default))
                    {
                        bool isTokenChanged = false;
                        if (parameters.TryGetValue("token", out string token))
                        {
                            if (_currentToken != token)
                            {
                                _currentToken = token;
                                isTokenChanged = true;
                            }
                        }
#if DEBUG
                        else
                        {
                            Debug.Assert(false, "required 'token' parameter in FolderListupPage navigation.");
                        }
#endif

                        bool isPathChanged = false;
                        if (parameters.TryGetValue("path", out string path))
                        {
                            var unescapedPath = Uri.UnescapeDataString(path);
                            if (_currentPath != unescapedPath)
                            {
                                isPathChanged = true;
                                _currentPath = unescapedPath;
                            }
                        }

                        // 以下の場合に表示内容を更新する
                        //    1. 表示フォルダが変更された場合
                        //    2. 前回の更新が未完了だった場合
                        if (isTokenChanged || isPathChanged)
                        {
                            {
                                var items = new[] { FolderItems, ArchiveFileItems, ImageFileItems }
                                    .SelectMany(x => x)
                                    .ToArray();
                                
                                FolderItems.Clear();
                                ArchiveFileItems.Clear();
                                ImageFileItems.Clear();

                                items.AsParallel().ForAll(x => x.Dispose());
                            }

                            if (isTokenChanged)
                            {
                                _tokenGettingFolder = await _sourceStorageItemsRepository.GetFolderAsync(token);
                            }

                            if (_tokenGettingFolder == null)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }

                            var currentPathItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);
                            _currentItem = currentPathItem;
                            DisplayCurrentPath = _currentItem.Path;

                            await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                        }
                        else if (!_isCompleteEnumeration)
                        {
                            await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                        }
                        else
                        {
                            HasFileItem = ImageFileItems.Any();
                        }
                    }
                }
                else if (mode == NavigationMode.Refresh)
                {
                    HasFileItem = false;
                    using (await _NavigationLock.LockAsync(default))
                    {
                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else if (!_isCompleteEnumeration
                    || _LastIsImageFileThumbnailEnabled != _folderListingSettings.IsImageFileThumbnailEnabled
                    || _LastIsArchiveFileThumbnailEnabled != _folderListingSettings.IsArchiveFileThumbnailEnabled
                    || _LastIsFolderThumbnailEnabled != _folderListingSettings.IsFolderThumbnailEnabled
                    )
                {
                    using (await _NavigationLock.LockAsync(default))
                    {
                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else
                {
                    // 前回読み込みキャンセルしていたものを改めて読み込むように
                    ImageFileItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                    ArchiveFileItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                    FolderItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                }
            }
            finally
            {
                NowProcessing = false;
            }

            await base.OnNavigatedToAsync(parameters);
        }


        #region Refresh Item

        static FastAsyncLock _RefreshLock = new FastAsyncLock();
        private async Task RefreshFolderItems(CancellationToken ct)
        {
            using var _ = await _RefreshLock.LockAsync(ct);


            _isCompleteEnumeration = false;
            try
            {
                if (_currentItem is StorageFolder folder)
                {
                    Debug.WriteLine(folder.Path);
                    await RefreshFolderItems(folder, ct);
                }
                else if (_currentItem is StorageFile file)
                {
                    Debug.WriteLine(file.Path);
                    await RefreshFolderItems(file, ct);
                }
                else if (_tokenGettingFolder != null)
                {
                    Debug.WriteLine(_tokenGettingFolder.Path);
                    await RefreshFolderItems(_tokenGettingFolder, ct);
                }
                else
                {
                    throw new NotSupportedException();
                }

                _isCompleteEnumeration = true;
            }
            catch (OperationCanceledException)
            {

            }
        }

        
        private async ValueTask RefreshFolderItems(IStorageItem storageItem, CancellationToken ct)
        {
            FolderItems.Clear();
            ArchiveFileItems.Clear();
            ImageFileItems.Clear();
            var result = await _imageCollectionManager.GetImageSourcesAsync(storageItem, ct);

            if (result.Images?.Any() != true)
            {
                return;
            }

            List<StorageItemViewModel> unsortedFileItems = new List<StorageItemViewModel>();
            foreach (var folderItem in result.Images)
            {
                ct.ThrowIfCancellationRequested();
                var item = new StorageItemViewModel(folderItem, _currentToken, _sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings);
                if (item.Type == StorageItemTypes.Folder)
                {
                    FolderItems.Add(item);
                }
                else if (item.Type == StorageItemTypes.Image)
                {
                    unsortedFileItems.Add(item);
                }
                else if (item.Type == StorageItemTypes.Archive)
                {
                    ArchiveFileItems.Add(item);
                }
            }

            var sortedFileItems = SelectedFileSortType.Value switch
            {
                FileSortType.TitleAscending => unsortedFileItems.OrderBy(x => x.Name),
                FileSortType.TitleDecending => unsortedFileItems.OrderByDescending(x => x.Name),
                FileSortType.UpdateTimeAscending => unsortedFileItems.OrderBy(x => x.DateCreated),
                FileSortType.UpdateTimeDecending => unsortedFileItems.OrderByDescending(x => x.DateCreated),
                _ => throw new NotSupportedException(),
            };


            using (FileItemsView.DeferRefresh())
            {
                ImageFileItems.AddRange(sortedFileItems);
            }

            HasFileItem = ImageFileItems.Any();
        }


        #endregion


        #region Navigation

        private INavigationService _navigationService;



        #endregion

        #region FileSortType

        private DelegateCommand<object> _ChangeFileSortCommand;
        public DelegateCommand<object> ChangeFileSortCommand =>
            _ChangeFileSortCommand ??= new DelegateCommand<object>(async sort => 
            {
                FileSortType? sortType = null;
                if (sort is int num)
                {
                    sortType = (FileSortType)num;
                }
                else if (sort is FileSortType sortTypeExact)
                {
                    sortType = sortTypeExact;
                }

                if (sortType.HasValue)
                {
                    SelectedFileSortType.Value = sortType.Value;

                    using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token))
                    {
                        if (ImageFileItems.Any())
                        {
                            var items = ImageFileItems.ToArray();
                            ImageFileItems.Clear();
                            ImageFileItems.Reverse().ForEach(x => x.ClearImage());

                            var sortedFileItems = SelectedFileSortType.Value switch
                            {
                                FileSortType.TitleAscending => items.OrderBy(x => x.Name),
                                FileSortType.TitleDecending => items.OrderByDescending(x => x.Name),
                                FileSortType.UpdateTimeAscending => items.OrderBy(x => x.DateCreated),
                                FileSortType.UpdateTimeDecending => items.OrderByDescending(x => x.DateCreated),
                                _ => throw new NotSupportedException(),
                            };

                            using (FileItemsView.DeferRefresh())
                            {
                                ImageFileItems.AddRange(sortedFileItems);
                            }
                        }

                        RaisePropertyChanged(nameof(ImageFileItems));
                    }
               }
            });


        #endregion
    }

    public abstract class FolderItemsGroupBase
    {
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
    public sealed class FolderFolderItemsGroup : FolderItemsGroupBase
    {

    }

    public sealed class FileFolderItemsGroup : FolderItemsGroupBase
    {

    }

    class FolderListupPageParameter
    {
        public string Token { get; set; }
        public string Path { get; set; }
    }
    
}
