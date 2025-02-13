﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TsubameViewer.Presentation.Services
{
    public interface IViewLocator
    {
        Type ResolveView(string viewName);
    }

    public sealed class ViewLocator : IViewLocator
    {
        public Type ResolveView(string viewName)
        {
            return Type.GetType($"TsubameViewer.Presentation.Views.{viewName}");
        }
    }

}
