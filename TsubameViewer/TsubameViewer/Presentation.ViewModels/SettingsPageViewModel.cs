﻿using I18NPortable;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Helpers;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views.Converters;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Xamarin.Essentials;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SettingsPageViewModel : NavigationAwareViewModelBase, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly ApplicationSettings _applicationSettings;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ImageViewerSettings _imageViewerPageSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly WindowsTriggers _windowsTriggers;

        public SettingsGroupViewModel[] SettingGroups { get; }
        public SettingsGroupViewModel[] AdvancedSettingGroups { get; }

        public RelayCommand AppInfoCopyToClipboard { get; }

        CancellationTokenSource _navigationCts;

        public bool IsForceXboxAppearanceModeEnabled
        {
            get => _applicationSettings.ForceXboxAppearanceModeEnabled;
            set
            {
                _applicationSettings.ForceXboxAppearanceModeEnabled = value;
                App.Current.Resources["DebugTVMode"] = value;
            }
        }

        public string ReportUserEnvString { get; }
        public BackNavigationCommand BackNavigationCommand { get; }

        public SettingsPageViewModel(
            IMessenger messenger,
            ApplicationSettings applicationSettings,
            FolderListingSettings folderListingSettings,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ImageViewerSettings imageViewerPageSettings,
            ThumbnailManager thumbnailManager,
            WindowsTriggers windowsTriggers,
            BackNavigationCommand backNavigationCommand
            )
        {
            _messenger = messenger;
            _applicationSettings = applicationSettings;
            _folderListingSettings = folderListingSettings;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _imageViewerPageSettings = imageViewerPageSettings;
            _thumbnailManager = thumbnailManager;
            _windowsTriggers = windowsTriggers;
            BackNavigationCommand = backNavigationCommand;
            _IsThumbnailDeleteButtonActive = new ReactiveProperty<bool>();
            _ThumbnailImagesCacheSizeText = new ReactivePropertySlim<string>();
            
            AppInfoCopyToClipboard = new RelayCommand(async () =>
            {
                var data = new DataPackage();
                data.SetText(ReportUserEnvString);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            });

            SettingGroups = new[]
            {                
                new SettingsGroupViewModel
                {
                    Label = "SourceFoldersSettings".Translate(),
                    Items =
                    {
                        new StoredFoldersSettingItemViewModel(_messenger, _sourceStorageItemsRepository),
                    }
                },
                new SettingsGroupViewModel
                {
                    Label = "ThumbnailImageSettings".Translate(),
                    Items =
                    {
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateImageFileThumbnail".Translate(), _folderListingSettings, x => x.IsImageFileGenerateThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateFolderThumbnail".Translate(), _folderListingSettings, x => x.IsFolderGenerateThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveFileThumbnail".Translate(), _folderListingSettings, x => x.IsArchiveFileGenerateThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveEntryThumbnail".Translate(), _folderListingSettings, x => x.IsArchiveEntryGenerateThumbnailEnabled),
                        new UpdatableTextSettingItemViewModel("ThumbnailCacheSize".Translate(), _ThumbnailImagesCacheSizeText),
                        new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), () => DeleteThumnnailsAsync()),
                    }
                },
                new SettingsGroupViewModel
                {
                    Label = "ImageViewerSettings".Translate(),
                    Items =
                    {
                        new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnablePrefetch".Translate(), _imageViewerPageSettings, x => x.IsEnablePrefetch),
                        new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsReverseImageFliping_MouseWheel".Translate(), _imageViewerPageSettings, x => x.IsReverseImageFliping_MouseWheel),
                        new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnableDoubleView".Translate(), _imageViewerPageSettings, x => x.IsEnableDoubleView),
                    }
                },
                new SettingsGroupViewModel
                {
                    Label = "GeneralUISettings".Translate(),
                    Items =
                    {
                        new ThemeSelectSettingItemViewModel("ApplicationTheme".Translate(), _applicationSettings, _messenger),
                        new LocaleSelectSettingItemViewModel("OverrideLocale".Translate(), _applicationSettings),

                    }
                },
            };

            AdvancedSettingGroups = new[]
            {
                new SettingsGroupViewModel
                {
                    /*
                    Label = "ThumbnailImageSettings".Translate(),
                    Items =
                    {
                        new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), () => _ = DeleteThumnnailsAsync()),
                        new UpdatableTextSettingItemViewModel("ThumbnailCacheSize".Translate(), _ThumbnailImagesCacheSizeText)
                    }
                    */
                },
            };

            StringBuilder sb = new StringBuilder();
            sb.Append(SystemInformation.Instance.ApplicationName)
                .Append(" v").Append(SystemInformation.Instance.ApplicationVersion.ToFormattedString())
                .AppendLine();
            sb.Append(SystemInformation.Instance.OperatingSystem).Append(" ").Append(SystemInformation.Instance.OperatingSystemArchitecture)
                .Append("(").Append(SystemInformation.Instance.OperatingSystemVersion).Append(")")
                .Append(" ").Append(_windowsTriggers.DeviceFamily.ToString())
                ;
            ReportUserEnvString = sb.ToString();
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            Dispose();

            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = null;

            base.OnNavigatedFrom(parameters);
        }

        public void Dispose()
        {
            Array.ForEach(SettingGroups, x => x.Dispose());
            Array.ForEach(AdvancedSettingGroups, x => (x as IDisposable)?.Dispose());
        }

        private async Task DeleteThumnnailsAsync()
        {
            _ThumbnailImagesCacheSizeText.Value = string.Empty;

            _IsThumbnailDeleteButtonActive.Value = false;
            await _thumbnailManager.DeleteAllThumnnailsAsync();

            _ = RefreshThumbnailFilesSizeAsync(_navigationCts?.Token ?? CancellationToken.None);
        }

        ReactiveProperty<bool> _IsThumbnailDeleteButtonActive;
        ReactivePropertySlim<string> _ThumbnailImagesCacheSizeText;
        

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();            
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;

            _IsThumbnailDeleteButtonActive.Value = true;

            _ = RefreshThumbnailFilesSizeAsync(ct);
            // base.OnNavigatedToAsync(parameters);
        }

        private async Task RefreshThumbnailFilesSizeAsync(CancellationToken ct)
        {
            try
            {
                var size = (ulong) await Task.Run(() => _thumbnailManager.ComputeUsingSize(), ct);

                _ThumbnailImagesCacheSizeText.Value = ToUserFiendlyFileSizeText(size) + "B";
            }
            catch (OperationCanceledException)
            {

            }
        }
        private static string ToUserFiendlyFileSizeText(ulong size)
        {
            var conv = new ToKMGTPEZYConverter();
            return (string)conv.Convert(size, typeof(string), null, CultureInfo.CurrentCulture.Name);
        }


    }

    public abstract class SettingItemViewModelBase : ObservableObject, IDisposable
    {
        public bool IsVisible { get; set; } = true;

        public virtual void Dispose() { }
    }

    public sealed class SettingsGroupViewModel : IDisposable
    {
        public bool IsVisible => Items?.Any(x => x.IsVisible) ?? false;

        public string Label { get; set; }
        public List<SettingItemViewModelBase> Items { get; set; } = new List<SettingItemViewModelBase>();

        public void Dispose() 
        {
            Items.ForEach(x => x.Dispose());
        }
    }


    public sealed class StoredFoldersSettingItemViewModel :  SettingItemViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _SourceStorageItemsRepository;

        public ObservableCollection<StoredFolderViewModel> Folders { get; }
        public ObservableCollection<StoredFolderViewModel> TempFiles { get; }

        public StoredFoldersSettingItemViewModel(IMessenger messenger, SourceStorageItemsRepository SourceStorageItemsRepository)
        {
            _messenger = messenger;
            _SourceStorageItemsRepository = SourceStorageItemsRepository;
            Folders = new ObservableCollection<StoredFolderViewModel>();
            TempFiles = new ObservableCollection<StoredFolderViewModel>();

            Init();
        }

        async void Init()
        {            
            await foreach (var item in _SourceStorageItemsRepository.GetParsistantItems())
            {
                if (_SourceStorageItemsRepository.IsIgnoredPathExact(item.item.Path)) { continue; }

                Folders.Add(new StoredFolderViewModel(_messenger, _SourceStorageItemsRepository, this)
                {
                    Item = item.item,
                    FolderName = item.item.Name,
                    Path = item.item.Path,
                    Token = item.token,
                });
            }

            await foreach (var item in _SourceStorageItemsRepository.GetTemporaryItems())
            {
                if (_SourceStorageItemsRepository.IsIgnoredPathExact(item.item.Path)) { continue; }

                TempFiles.Add(new StoredFolderViewModel(_messenger, _SourceStorageItemsRepository, this)
                {
                    Item = item.item,
                    FolderName = item.item.Name,
                    Path = item.item.Path,
                    Token = item.token,
                });
            }
        }

        internal void RemoveItem(StoredFolderViewModel childVM)
        {
            Folders.Remove(childVM);
            TempFiles.Remove(childVM);
        }
    }

    public class StoredFolderViewModel
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _SourceStorageItemsRepository;
        private readonly StoredFoldersSettingItemViewModel _parentVM;

        public StoredFolderViewModel(IMessenger messenger, SourceStorageItemsRepository SourceStorageItemsRepository, StoredFoldersSettingItemViewModel parentVM)
        {
            _messenger = messenger;
            _SourceStorageItemsRepository = SourceStorageItemsRepository;
            _parentVM = parentVM;
        }

        public string FolderName { get; set; }
        public string Path { get; set; }
        public string Token { get; set; }
        public IStorageItem Item { get; set; }


        private RelayCommand _DeleteStoredFolderCommand;
        public RelayCommand DeleteStoredFolderCommand =>
            _DeleteStoredFolderCommand ??= new RelayCommand(async () => 
            {
                _messenger.Send<SourceStorageItemIgnoringRequestMessage>(new(Path));

                //_SourceStorageItemsRepository.RemoveFolder(Token);
                _parentVM.RemoveItem(this);

                /*
                var dialog = new MessageDialog(
                    "ConfirmRemoveSourceFolderFromAppDescription".Translate(),
                    $"ConfirmRemoveSourceFolderFromAppWithFolderName".Translate(FolderName)
                    );

                dialog.Commands.Add(new UICommand("RemoveSourceFolderFromApp".Translate(), _ => 
                {
                    _SourceStorageItemsRepository.RemoveFolder(Token);
                    _parentVM.RemoveItem(this);
                }));

                dialog.Commands.Add(new UICommand("Cancel".Translate()));
                dialog.CancelCommandIndex = 1;
                dialog.DefaultCommandIndex = 0;

                var result = await dialog.ShowAsync();
                */
            });
    }



    public interface IToggleSwitchSettingItemViewModel
    {
        string Label { get; }
        ReactiveProperty<bool> ValueContainer { get; }
    }

    public class ToggleSwitchSettingItemViewModel<T> : SettingItemViewModelBase, IToggleSwitchSettingItemViewModel , IDisposable
        where T : INotifyPropertyChanged
    {
        public ToggleSwitchSettingItemViewModel(string label, T value, Expression<Func<T, bool>> expression)
        {
            ValueContainer = value.ToReactivePropertyAsSynchronized(expression);
            Label = label;
        }

        public ReactiveProperty<bool> ValueContainer { get; }
        public string Label { get; }

        public void Dispose()
        {
            ((IDisposable)ValueContainer).Dispose();
        }
    }

    public class UpdatableTextSettingItemViewModel : SettingItemViewModelBase, IDisposable 
    {
        public string Label { get; }
        public IReadOnlyReactiveProperty<string> Text { get; }
        public UpdatableTextSettingItemViewModel(string label, IObservable<string> textObservable)
        {
            Label = label;
            Text = textObservable.ToReadOnlyReactivePropertySlim();
        }

        public void Dispose()
        {
            Text.Dispose();
        }
    }

    public class ButtonSettingItemViewModel : SettingItemViewModelBase, IDisposable
    {
        public ButtonSettingItemViewModel(string label, Func<Task> buttonAction)
        {
            Label = label;
            ActionCommand = new AsyncReactiveCommand();
            ActionCommand.Subscribe(buttonAction);
        }

        public ButtonSettingItemViewModel(string label, Func<Task> buttonAction, IObservable<bool> canExecuteObservable)
        {
            Label = label;
            ActionCommand = canExecuteObservable.ToAsyncReactiveCommand();
            ActionCommand.Subscribe(buttonAction);
        }

        public ButtonSettingItemViewModel(string label, Action buttonAction, IObservable<bool> canExecuteObservable)
        {
            Label = label;
            ActionCommand = canExecuteObservable.ToAsyncReactiveCommand();
            ActionCommand.Subscribe(_ => 
            {
                buttonAction();
                return Task.CompletedTask;
            });
        }

        public string Label { get; }
        public AsyncReactiveCommand ActionCommand { get; }

        public void Dispose()
        {
            ((IDisposable)ActionCommand).Dispose();
        }
    }

    public class ThemeSelectSettingItemViewModel : SettingItemViewModelBase, IDisposable
    {
        private readonly IMessenger _messenger;

        public ThemeSelectSettingItemViewModel(string label, ApplicationSettings applicationSettings, IMessenger messenger)
        {
            Label = label;
            _messenger = messenger;
            SelectedTheme = applicationSettings.ToReactivePropertyAsSynchronized(x => x.Theme);

            _themeChangedSubscriber = SelectedTheme.Subscribe(theme => 
            {
                _messenger.Send<ThemeChangeRequestMessage>(new (theme));
            });
        }

        public ReactiveProperty<ApplicationTheme> SelectedTheme { get; }

        public IReadOnlyList<ApplicationTheme> ThemeItems { get; } = new[] { ApplicationTheme.Default, ApplicationTheme.Light, ApplicationTheme.Dark };

        IDisposable _themeChangedSubscriber;

        public string Label { get; }

        public void Dispose()
        {
            ((IDisposable)SelectedTheme).Dispose();
            _themeChangedSubscriber.Dispose();
        }
    }

    public class LocaleSelectSettingItemViewModel : SettingItemViewModelBase, IDisposable
    {
        private string _currentLocale = I18NPortable.I18N.Current.Locale;
        public LocaleSelectSettingItemViewModel(string label, ApplicationSettings applicationSettings)
        {
            Label = label;
            SelectedLocale = applicationSettings.ToReactivePropertyAsSynchronized(x => x.Locale);

            _themeChangedSubscriber = SelectedLocale.Subscribe(locale =>
            {
                if (string.IsNullOrEmpty(locale)) { return; }

                I18NPortable.I18N.Current.Locale = locale;
                IsRequireRestart = _currentLocale != locale;
                RestartTextTranslated = "RequireRestartApplicationToRefrectSettings".Translate();
            });
        }

        private string _RestartTextTranslated;
        public string RestartTextTranslated
        {
            get { return _RestartTextTranslated; }
            set { SetProperty(ref _RestartTextTranslated, value); }
        }

        private bool _isRequireRestart;
        public bool IsRequireRestart
        {
            get { return _isRequireRestart; }
            set { SetProperty(ref _isRequireRestart, value); }
        }

        public ReactiveProperty<string> SelectedLocale { get; }

        public IReadOnlyList<PortableLanguage> Locales { get; } = I18NPortable.I18N.Current.Languages;

        IDisposable _themeChangedSubscriber;

        public string Label { get; }

        public void Dispose()
        {
            ((IDisposable)SelectedLocale).Dispose();
            _themeChangedSubscriber.Dispose();
        }

        private RelayCommand _RestartApplicationCommand;
        public RelayCommand RestartApplicationCommand =>
            _RestartApplicationCommand ?? (_RestartApplicationCommand = new RelayCommand(ExecuteRestartApplicationCommand));

        void ExecuteRestartApplicationCommand()
        {
            _ = Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync("");
        }
    }
}
