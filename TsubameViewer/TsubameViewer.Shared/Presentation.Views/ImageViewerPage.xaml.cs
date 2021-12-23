﻿using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using Prism.Commands;
using Prism.Ioc;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Uno;
using Uno.Disposables;
using Uno.Threading;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageViewerPage : Page
    {
        public ImageViewerPage()
        {
            this.InitializeComponent();

            Loaded += ResetAnimationUIContainer_Loaded1;
            Unloaded += TapAndController_Unloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as ImageViewerPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private ImageViewerPageViewModel _vm { get; set; }

        private void TapAndController_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= ImageViewerPage_BackRequested;
        }

        private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            AnimationBuilder.Create()
                .Translation(Axis.Y, (float)AnimationUICommandBar.ActualHeight)
                .Start(AnimationUICommandBar);
            AnimationBuilder.Create()
                .Opacity(0, duration: TimeSpan.FromMilliseconds(1))
                .Start(AnimationUIContainer);

            SwipeProcessScreen.Tapped += SwipeProcessScreen_Tapped;
            SwipeProcessScreen.ManipulationMode = ManipulationModes.TranslateY | ManipulationModes.TranslateX;
            SwipeProcessScreen.ManipulationStarting += SwipeProcessScreen_ManipulationStarting;
            SwipeProcessScreen.ManipulationStarted += SwipeProcessScreen_ManipulationStarted;
            SwipeProcessScreen.ManipulationCompleted += SwipeProcessScreen_ManipulationCompleted;

            SystemNavigationManager.GetForCurrentView().BackRequested += ImageViewerPage_BackRequested;
        }

        private void ImageViewerPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (IsOpenBottomMenu)
            {
                ToggleOpenCloseBottomUI();
            }
            else
            {
                (_vm.BackNavigationCommand as ICommand).Execute(null);
            }
        }

        private void SwipeProcessScreen_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var pt = e.GetPosition(RootGrid);

            if (isOnceSkipTapped)
            {
                var bottomUIItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, AnimationUICommandBar);
                if (bottomUIItems.Any()) { return; }

                CloseBottomUI();
                isOnceSkipTapped = false;
                e.Handled = true;
                return;
            }
            
            var uiItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, UIContainer);
            foreach (var item in uiItems)
            {
                if (item == RightPageMoveButton)
                {
                    if (RightPageMoveButton.Command?.CanExecute(null) ?? false)
                    {
                        RightPageMoveButton.Command.Execute(null);
                    }
                }
                else if (item == LeftPageMoveButton)
                {
                    if (LeftPageMoveButton.Command?.CanExecute(null) ?? false)
                    {
                        LeftPageMoveButton.Command.Execute(null);
                    }
                }
                else if (item == ToggleBottomMenuButton)
                {
                    if (ToggleBottomMenuButton.Command?.CanExecute(null) ?? false)
                    {
                        ToggleBottomMenuButton.Command.Execute(null);
                    }
                }
            }
        }

        private void SwipeProcessScreen_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            Debug.WriteLine(e.Cumulative.Translation.Y);
        }


        bool isOnceSkipTapped = false;
        private void SwipeProcessScreen_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            if (AnimationUIContainer.Opacity == 1.0) 
            {
                e.Handled = true;
                isOnceSkipTapped = true;
                return;
            }
        }

        private void SwipeProcessScreen_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Cumulative.Translation.X > 60
                || e.Velocities.Linear.X > 0.75
                )
            {
                // 右スワイプ
                LeftPageMoveButton.Command.Execute(null);
            }
            else if (e.Cumulative.Translation.X < -60
                || e.Velocities.Linear.X < -0.75
                )
            {
                // 左スワイプ
                RightPageMoveButton.Command.Execute(null);
            }
            else if (e.Cumulative.Translation.Y < -60
                || e.Velocities.Linear.Y < -0.25
                )
            {
                _ = CompleteOpenBottomUI();
                e.Handled = true;
            }
            else
            {
                CloseBottomUI();
                e.Handled = true;
            }
        }

        private readonly AnimationBuilder _HideUIContainerAb = AnimationBuilder.Create()
                .Opacity(0, duration: TimeSpan.FromMilliseconds(175));
        private readonly AnimationBuilder _HideUICommandBarAb = AnimationBuilder.Create();
                

        private void CloseBottomUI()
        {
            _HideUIContainerAb
                .Start(AnimationUIContainer);
            
            _HideUICommandBarAb
                .Translation(Axis.Y, AnimationUICommandBar.ActualHeight, duration: TimeSpan.FromMilliseconds(175))
                .Start(AnimationUICommandBar);
        }

        private readonly AnimationBuilder _ShowUIContainer = AnimationBuilder.Create()
                .Opacity(1.0, duration: TimeSpan.FromMilliseconds(175));

        private readonly AnimationBuilder _ShowUICommandBarAb = AnimationBuilder.Create()
            .Translation(Axis.Y, 0, duration: TimeSpan.FromMilliseconds(175));

        private async Task CompleteOpenBottomUI()
        {
            _ShowUIContainer
                .Start(AnimationUIContainer);
            await _ShowUICommandBarAb
                .StartAsync(AnimationUICommandBar);
        }




        public bool IsOpenBottomMenu
        {
            get { return (bool)GetValue(IsOpenBottomMenuProperty); }
            set { SetValue(IsOpenBottomMenuProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsOpenBottomMenu.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsOpenBottomMenuProperty =
            DependencyProperty.Register("IsOpenBottomMenu", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));


        // コントローラー操作用
        public async void ToggleOpenCloseBottomUI()
        {
            IsOpenBottomMenu = !IsOpenBottomMenu;
            if (IsOpenBottomMenu)
            {
                ImageNavigationFlyoutButton.Focus(FocusState.Keyboard);
                await CompleteOpenBottomUI();
            }
            else
            {
                CloseBottomUI();
            }
        }

        private DelegateCommand _toggleBottomMenuCommand;
        public DelegateCommand ToggleBottomMenuCommand =>
            _toggleBottomMenuCommand ?? (_toggleBottomMenuCommand = new DelegateCommand(ExecuteToggleBottomMenuCommand, () => true) { IsActive = true });

        void ExecuteToggleBottomMenuCommand()
        {
            ToggleOpenCloseBottomUI();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            if ((bool)App.Current.Resources["DebugTVMode"] is true)
            {
                Window.Current.SetTitleBar(DraggableTitleBarArea_Xbox);
            }
            else
            {
                Window.Current.SetTitleBar(DraggableTitleBarArea_Desktop);
            }
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0xcf, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x9f, 0xff, 0xff, 0xff);

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = true;

            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(PageTransisionHelper.ImageJumpConnectedAnimationName);
            if (animation != null)
            {
                // Note: CancelletionTokenSourceにタイムアウトを指定した形で実装すると
                // Win32Exceptionでアプリがクラッシュする
                // とにかく待って処理する男気実装でいくしかない

                await this.ObserveDependencyProperty(DataContextProperty)
                    .Where(x => _vm is not null)
                    .Take(1)
                    .ToAsyncOperation()
                    .AsTask();

                await _vm.ObserveProperty(x => x.DisplayImages_0, isPushCurrentValueAtFirst: false)
                    .Take(1)
                    .ToAsyncOperation()
                    .AsTask();

                if (_vm.DisplayImages_0.Length == 1)
                {
                    ImageItemsControl_0.Opacity = 0.001;
                    var image = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                       h => ImageItemsControl_0.ElementPrepared += h,
                       h => ImageItemsControl_0.ElementPrepared -= h
                       )
                       .Select(x => x.EventArgs.Element)
                       .Take(1)
                       .ToAsyncOperation()
                       .AsTask();

                    int count = 0;
                    while (image.ActualSize is { X: 0, Y: 0 })
                    {
                        await Task.Delay(1);
                        count++;
                        if (count == 750)
                        {
                            animation.Cancel();
                            return;
                        }
                    }

                    animation.TryStart(ImageItemsControl_0, new[] { image });
                    ImageItemsControl_0.Opacity = 1.0;
                }
                else
                {
                    ImageItemsControl_0.Opacity = 0.001;
                    var images = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                       h => ImageItemsControl_0.ElementPrepared += h,
                       h => ImageItemsControl_0.ElementPrepared -= h
                       )
                       .Select(x => x.EventArgs.Element)
                       .Take(2)
                       .Buffer(2)
                       .ToAsyncOperation();
                       
                    int count = 0;
                    while (images.All(image => image.ActualSize is { X: 0, Y: 0 }))
                    {
                        await Task.Delay(1);
                        count++;
                        if (count == 750)
                        {
                            animation.Cancel();
                            return;
                        }
                    }

                    animation.TryStart(ImageItemsControl_0, images);
                    ImageItemsControl_0.Opacity = 1.0;
                }
            }
            else
            {
                AnimationBuilder.Create()
                    .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
                    .Start(ImageItemsControl_0);

                await this.ObserveDependencyProperty(DataContextProperty)
                    .Where(x => _vm is not null)
                    .Take(1)
                    .ToAsyncOperation()
                    .AsTask();

                await _vm.ObserveProperty(x => x.DisplayImages_0, isPushCurrentValueAtFirst: false)
                    .Take(1)
                    .ToAsyncOperation()
                    .AsTask();

                if (_vm.DisplayImages_0.Length == 1)
                {
                    var image = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                       h => ImageItemsControl_0.ElementPrepared += h,
                       h => ImageItemsControl_0.ElementPrepared -= h
                       )
                       .Select(x => x.EventArgs.Element)
                       .Take(1)
                       .ToAsyncOperation()
                       .AsTask();

                    int count = 0;
                    while (image.ActualSize is { X: 0, Y: 0 })
                    {
                        await Task.Delay(1);
                        count++;
                        if (count == 750)
                        {
                            animation.Cancel();
                            return;
                        }
                    }

                    AnimationBuilder.Create()
                        .CenterPoint(new Vector2((float)ImageItemsControl_0.ActualWidth * 0.5f, (float)ImageItemsControl_0.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                        .Scale()
                            .TimedKeyFrames(ke => 
                            {
                                ke.KeyFrame(TimeSpan.FromMilliseconds(0), new (0.9f));
                                ke.KeyFrame(TimeSpan.FromMilliseconds(250), new(1.0f));
                            })
                        .Opacity(1.0, delay: TimeSpan.FromMilliseconds(10), duration: TimeSpan.FromMilliseconds(250))
                        .Start(ImageItemsControl_0);
                }
                else
                {
                    AnimationBuilder.Create()
                        .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
                        .Start(ImageItemsControl_0);
                    var images = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                       h => ImageItemsControl_0.ElementPrepared += h,
                       h => ImageItemsControl_0.ElementPrepared -= h
                       )
                       .Select(x => x.EventArgs.Element)
                       .Take(2)
                       .Buffer(2)
                       .ToAsyncOperation();

                    int count = 0;
                    while (images.All(image => image.ActualSize is { X: 0, Y: 0 }))
                    {
                        await Task.Delay(1);
                        count++;
                        if (count == 750)
                        {
                            animation.Cancel();
                            return;
                        }
                    }

                    AnimationBuilder.Create()
                        .CenterPoint(new Vector2((float)ImageItemsControl_0.ActualWidth * 0.5f, (float)ImageItemsControl_0.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                        .Scale()
                            .TimedKeyFrames(ke =>
                            {
                                ke.KeyFrame(TimeSpan.FromMilliseconds(0), new(0.9f));
                                ke.KeyFrame(TimeSpan.FromMilliseconds(250), new(1.0f));
                            })
                        .Opacity(1.0, delay: TimeSpan.FromMilliseconds(10), duration: TimeSpan.FromMilliseconds(250))
                        .Start(ImageItemsControl_0);
                }
            }

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            Window.Current.SetTitleBar(null);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = null;
            appView.TitleBar.ButtonHoverBackgroundColor = null;
            appView.TitleBar.ButtonInactiveBackgroundColor = null;
            appView.TitleBar.ButtonPressedBackgroundColor = null;

            appView.ExitFullScreenMode();

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = false;

            base.OnNavigatingFrom(e);
        }
    }
}
