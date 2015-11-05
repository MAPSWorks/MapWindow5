﻿// -------------------------------------------------------------------------------------------
// <copyright file="ILayoutView.cs" company="MapWindow OSS Team - www.mapwindow.org">
//  MapWindow OSS Team - 2015
// </copyright>
// -------------------------------------------------------------------------------------------

using MW5.Plugins.Mvp;
using MW5.Plugins.Printing.Controls.Layout;

namespace MW5.Plugins.Printing.Views.Abstract
{
    internal interface ILayoutView : IView<TemplateModel>
    {
        LayoutControl LayoutControl { get; }

        object MenuManager { get; }
    }
}