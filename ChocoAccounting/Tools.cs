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
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ChocoAccounting
{
    public static class Tools
    {
        public static void SetSelectorData(Selector selector, DbSet data, string displayMember, string valueMember)
        {
            selector.DataContext = null;
            selector.ItemsSource = null;

            selector.DisplayMemberPath = displayMember;
            selector.SelectedValuePath = valueMember;

            data.Load();
            selector.DataContext = data.Local;
            selector.ItemsSource = data.Local;
        }

        public static void SetSelectorData(Selector selector, DbSet data, string valueMember)
        {
            SetSelectorData(selector, data, string.Empty, valueMember);
        }

        public static void SetSelectorData(Selector selector, IList data, string displayMember, string valueMember)
        {
            selector.DataContext = null;
            selector.ItemsSource = null;

            selector.DisplayMemberPath = displayMember;
            selector.SelectedValuePath = valueMember;

            selector.DataContext = data;
            selector.ItemsSource = data;
        }

        public static void SetSelectorData(Selector selector, IList data, string valueMember)
        {
            SetSelectorData(selector, data, string.Empty, valueMember);
        }

        public static bool AreDatabaseFilesOnPathArray(string database, string[] filePaths)
        {
            foreach (string path in filePaths)
            {
                FileInfo info = new FileInfo(path);

                if (string.Equals(info.Name.ToLower(), (database + ".mdf").ToLower())
                    || string.Equals(info.Name.ToLower(), (database + "_log.ldf").ToLower()))
                    return true;
            }

            return false;
        }

        public static void DeleteDatabaseFilesFromPathArray(string database, string[] filePaths)
        {
            foreach (string path in filePaths)
            {
                FileInfo info = new FileInfo(path);

                if (string.Equals(info.Name.ToLower(), (database + ".mdf").ToLower())
                    || string.Equals(info.Name.ToLower(), (database + "_log.ldf").ToLower()))
                    File.Delete(path);
            }
        }

        public static Configuration GetAppConfiguration()
        {
            string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configFile = Path.Combine(appPath, "ChocoAccounting.exe.config");

            ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap();
            configFileMap.ExeConfigFilename = configFile;

            return ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
        }

        public static string GetConnectionStringAppConfig()
        {
            Configuration config = GetAppConfiguration();

            foreach (ConnectionStringSettings setting in config.ConnectionStrings.ConnectionStrings)
            {
                if (setting.Name.Equals("MovementsModel"))
                    return setting.ConnectionString;
            }

            return string.Empty;
        }

        public static string GetNewConnectionString(string newDatabase)
        {
            string connection = Properties.Settings.Default.ConnectionString;

            string dbFileNameParameter = "AttachDbFilename=|DataDirectory|\\";
            int i = connection.IndexOf(dbFileNameParameter);
            int j = connection.IndexOf(";", i);
            string dbFileNameOld = connection.Substring(i, j - i);
            connection = connection.Replace(dbFileNameOld, dbFileNameParameter + newDatabase + ".mdf");

            string initialCatalogParameter = "initial catalog=";
            i = connection.IndexOf(initialCatalogParameter);
            j = connection.IndexOf(";", i);
            string initialCatalogOld = connection.Substring(i, j - i);
            connection = connection.Replace(initialCatalogOld, initialCatalogParameter + newDatabase);

            return connection;
        }

        public static void ChangeConnectionStringConfig(string connectionString)
        {
            Properties.Settings.Default.ConnectionString = connectionString;
            Properties.Settings.Default.Save();
        }

        public static void ChangeDatabaseConnectionStringConfig(string newDatabase)
        {
            ChangeConnectionStringConfig(GetNewConnectionString(newDatabase));
        }

        public static void ChangeCultureConfig(CultureInfo culture)
        {
            TranslationSource.Instance.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            Properties.Settings.Default.Culture = culture.Name;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Searches the object for a child object of the specified type.
        /// </summary>
        /// <typeparam name="T">Type of the child object to be found.</typeparam>
        /// <param name="depObject">Parent object to be searched.</param>
        public static T GetChildOfType<T>(DependencyObject depObject) where T : DependencyObject
        {
            if (depObject == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObject); i++)
            {
                var child = VisualTreeHelper.GetChild(depObject, i);

                var result = (child as T) ?? GetChildOfType<T>(child);

                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Reloads the "select a date" message for a DatePicker control according to the current app language selection.
        /// </summary>
        /// <param name="datePickerControl">DatePicker control whose watermark will be reloaded.</param>
        public static void ReloadDatePickerWatermark(DatePicker datePickerControl)
        {
            if (datePickerControl == null)
                return;

            var childTextBox = GetChildOfType<DatePickerTextBox>(datePickerControl);

            if (childTextBox == null)
                return;

            var waterMark = childTextBox.Template.FindName("PART_Watermark", childTextBox) as ContentControl;

            if (waterMark == null)
                return;

            waterMark.Content = TranslationSource.Instance["DataPickerWatermark"];
        }

        /// <summary>
        /// Makes sure TextBox values complies with current culture decimal format.
        /// Must be called from TextChanged event.
        /// </summary>
        /// <param name="tb">TextBox instance.</param>
        /// <param name="changes">Changes collection from TextChanged event.</param>
        public static void EnsureProperTextBoxDecimalFormat(TextBox tb, ICollection<TextChange> changes)
        {
            string correctSeparator = string.Empty;
            string wrongSeparator = string.Empty;

            switch (TranslationSource.Instance.CurrentCulture.Name)
            {
                case "gl":
                    correctSeparator = ",";
                    wrongSeparator = ".";
                    break;

                case "es":
                    correctSeparator = ",";
                    wrongSeparator = ".";
                    break;

                default:
                    correctSeparator = ".";
                    wrongSeparator = ",";
                    break;
            }

            using (tb.DeclareChangeBlock())
            {
                foreach (var c in changes)
                {
                    if (c.AddedLength == 0)
                        continue;

                    tb.Select(c.Offset, c.AddedLength);

                    if (tb.SelectedText.Contains(wrongSeparator))
                        tb.SelectedText = tb.SelectedText.Replace(wrongSeparator, correctSeparator);

                    if (tb.SelectedText.Contains(" "))
                        tb.SelectedText = tb.SelectedText.Replace(" ", string.Empty);

                    tb.Select(c.Offset + c.AddedLength, 0);
                }
            }
        }
    }
}