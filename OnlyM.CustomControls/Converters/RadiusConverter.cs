﻿/*************************************************************************************
Adapted by Antony Corbett from the Xceed WPF Extended Toolkit.
Original copyright is shown below.
***********************************************************************************/

/*************************************************************************************
Extended WPF Toolkit
Copyright (C) 2007-2013 Xceed Software Inc.
This program is provided to you under the terms of the Microsoft Public
License (Ms-PL) as published at http://wpftoolkit.codeplex.com/license 
For more features, controls, and fast professional support,
pick up the Plus Edition at http://xceed.com/wpf_toolkit
Stay informed: follow @datagrid on Twitter or Like http://facebook.com/datagrids
***********************************************************************************/

namespace OnlyM.CustomControls.Converters
{
    using System;
    using System.Windows.Data;

    public class RadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double?)value * 2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
