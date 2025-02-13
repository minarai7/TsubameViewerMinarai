﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Presentation.Views.Dialogs;

namespace TsubameViewer.Presentation.Services
{
    public interface ISplitImageInputDialogService
    {
        Task<SplitImageInputDialogResult> GetSplitImageInputAsync();
    }

    public sealed class SplitImageInputDialogService : ISplitImageInputDialogService
    {
        public SplitImageInputDialogService()
        {

        }

        public async Task<SplitImageInputDialogResult> GetSplitImageInputAsync()
        {
            var dialog = new SplitImageInputDialog();

            return await dialog.GetSplitImageInputAsync();
        }
    }


}
