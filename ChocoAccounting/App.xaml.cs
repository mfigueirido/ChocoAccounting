/*
    This file is part of Choco Accounting.

    Choco Accounting is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Choco Accounting is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Choco Accounting. If not, see <http://www.gnu.org/licenses/>.
*/
using System.Windows;
using System.Windows.Controls;

namespace ChocoAccounting
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            EventManager.RegisterClassHandler(typeof(DatePicker),
                FrameworkElement.LoadedEvent, new RoutedEventHandler(DatePicker_Loaded));

            base.OnStartup(e);
        }

        public static void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            Tools.ReloadDatePickerWatermark(sender as DatePicker);
        }
    }
}
