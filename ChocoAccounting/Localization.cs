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
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace ChocoAccounting
{
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly TranslationSource instance = new TranslationSource();

        public static TranslationSource Instance
        {
            get
            {
                return instance;
            }
        }

        private readonly ResourceManager resourceManager = Properties.Resources.ResourceManager;
        private CultureInfo currentCulture = null;

        public string this[string key]
        {
            get { return resourceManager.GetString(key, currentCulture); }
        }

        public CultureInfo CurrentCulture
        {
            get
            {
                return currentCulture;
            }
            set
            {
                if (currentCulture != value)
                {
                    currentCulture = value;

                    var eventHandler = PropertyChanged;

                    if (eventHandler != null)
                    {
                        eventHandler.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class LocalizationExtension : Binding
    {
        public LocalizationExtension(string name) : base("[" + name + "]")
        {
            Mode = BindingMode.OneWay;
            Source = TranslationSource.Instance;
        }
    }

    public class CultureSelectionData
    {
        public CultureInfo Culture { get; set; }
        public string Name { get; set; }

        public CultureSelectionData(string cultureCode, string name)
        {
            Culture = new CultureInfo(cultureCode);
            Name = name;
        }

        public CultureSelectionData(CultureInfo culture, string name)
        {
            Culture = culture;
            Name = name;
        }
    }
}
