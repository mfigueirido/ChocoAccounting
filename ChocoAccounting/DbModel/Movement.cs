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
namespace ChocoAccounting.DbModel
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media;

    [Table("MovementSet")]
    public partial class Movement : INotifyPropertyChanged
    {
        // Need to implement INotifyPropertyChanged to notify the UI
        // the change on calculated properties
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private void MovementCategoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyQuantityRelatedPropertiesChanged();
        }

        private void MovementSubcategoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyQuantityRelatedPropertiesChanged();
        }

        private void NotifyDateRelatedPropertiesChanged()
        {
            NotifyPropertyChanged("MonthName");
            NotifyPropertyChanged("Month");
            NotifyPropertyChanged("Year");
        }

        private void NotifyQuantityRelatedPropertiesChanged()
        {
            NotifyPropertyChanged("QuantityWithoutTax");
            NotifyPropertyChanged("QuantityBrush");
            NotifyPropertyChanged("QuantityWithoutTaxBrush");
        }

        public int Id { get; set; }

        private DateTime _date;
        public DateTime Date
        {
            get
            {
                return _date;
            }
            set
            {
                _date = value;
                NotifyPropertyChanged();
                NotifyDateRelatedPropertiesChanged();
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get
            {
                return _quantity;
            }
            set
            {
                _quantity = value;
                NotifyPropertyChanged();
                NotifyQuantityRelatedPropertiesChanged();
            }
        }

        public string Remarks { get; set; }

        public bool Pending { get; set; }

        public int MovementTypeId { get; set; }

        public int MovementCategoryId { get; set; }

        public int? MovementSubcategoryId { get; set; }

        public int? EntityId { get; set; }

        public int? PersonId { get; set; }

        public int AccountId { get; set; }

        public virtual Account Account { get; set; }

        public virtual Entity Entity { get; set; }

        private MovementCategory _movementCategory;
        public virtual MovementCategory MovementCategory
        {
            get
            {
                return _movementCategory;
            }
            set
            {
                if (_movementCategory != null)
                    _movementCategory.PropertyChanged -= MovementCategoryPropertyChanged;

                _movementCategory = value;

                if (_movementCategory != null)
                    _movementCategory.PropertyChanged += MovementCategoryPropertyChanged;
            }
        }

        private MovementSubcategory _movementSubcategory;
        public virtual MovementSubcategory MovementSubcategory
        {
            get
            {
                return _movementSubcategory;
            }
            set
            {
                if (_movementSubcategory != null)
                    _movementSubcategory.PropertyChanged -= MovementSubcategoryPropertyChanged;

                _movementSubcategory = value;

                if (_movementSubcategory != null)
                    _movementSubcategory.PropertyChanged += MovementSubcategoryPropertyChanged;
            }
        }

        public virtual MovementType MovementType { get; set; }

        public virtual Person Person { get; set; }

        [NotMapped]
        public string MonthName
        {
            get
            {
                return Date.ToString("MMMM");
            }
            private set { }
        }

        [NotMapped]
        public int Month
        {
            get
            {
                return Date.Month;
            }
            private set { }
        }

        [NotMapped]
        public int Year
        {
            get
            {
                return Date.Year;
            }
            private set { }
        }

        [NotMapped]
        public decimal QuantityWithoutTax
        {
            get
            {
                decimal percentage = 0;

                if (MovementSubcategory != null
                    && MovementSubcategory.TaxPercentage != 0)
                {
                    percentage = MovementSubcategory.TaxPercentage;
                }
                else if (MovementCategory.TaxPercentage != 0)
                {
                    percentage = MovementCategory.TaxPercentage;
                }

                return Quantity - (Quantity * (percentage / 100m));
            }
            private set { }
        }

        [NotMapped]
        public Brush QuantityBrush
        {
            get
            {
                return GetBrushFromValue(Quantity);
            }
            private set { }
        }

        [NotMapped]
        public Brush QuantityWithoutTaxBrush
        {
            get
            {
                return GetBrushFromValue(QuantityWithoutTax);
            }
            private set { }
        }

        [NotMapped]
        public FontWeight QuantityFontWeight
        {
            get
            {
                return GetFontWeightFromValue(Quantity);
            }
            private set { }
        }

        [NotMapped]
        public FontWeight QuantityWithoutTaxFontWeight
        {
            get
            {
                return GetFontWeightFromValue(QuantityWithoutTax);
            }
            private set { }
        }

        private SolidColorBrush GetBrushFromValue(decimal value)
        {
            decimal maxQuantity = 200;
            decimal clampedQuantity = 0;

            if (value > maxQuantity)
                clampedQuantity = maxQuantity;
            else
                clampedQuantity = value;

            byte colour = (byte)(255m / (maxQuantity / clampedQuantity));

            if (colour < 150)
                colour = 150;
            else if (MovementTypeId == 2 && colour > 175)
                colour = 175;

            if (MovementTypeId == 1)
                return new SolidColorBrush(Color.FromRgb(colour, 0, 0));
            else
                return new SolidColorBrush(Color.FromRgb(0, colour, 0));
        }

        private FontWeight GetFontWeightFromValue(decimal value)
        {
            decimal maxQuantity = 600;
            decimal clampedQuantity = 0;

            if (value > maxQuantity)
                clampedQuantity = maxQuantity;
            else
                clampedQuantity = value;

            int weight = (int)(999 / (maxQuantity / clampedQuantity));

            if (weight < FontWeights.Normal.ToOpenTypeWeight())
                return FontWeights.Normal;
            else
                return FontWeight.FromOpenTypeWeight(weight);
        }
    }
}
