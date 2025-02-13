﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Presentation.Views.UINavigation;

namespace TsubameViewer.Presentation.Views.Helpers
{
    public sealed class FocusHelper
    {
        private readonly ApplicationSettings _applicationSettings;

        public FocusHelper(ApplicationSettings applicationSettings)
        {
            _applicationSettings = applicationSettings;
        }

        public bool IsRequireSetFocus()
        {
            return (Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                || Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox"
                || _applicationSettings.IsUINavigationFocusAssistanceEnabled)
                && UINavigationManager.NowControllerConnected
                ;
        }
    }
}
